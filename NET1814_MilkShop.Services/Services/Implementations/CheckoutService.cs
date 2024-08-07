using System.Data;
using Microsoft.EntityFrameworkCore;
using NET1814_MilkShop.Repositories.CoreHelpers.Constants;
using NET1814_MilkShop.Repositories.CoreHelpers.Enum;
using NET1814_MilkShop.Repositories.Data.Entities;
using NET1814_MilkShop.Repositories.Models;
using NET1814_MilkShop.Repositories.Models.CheckoutModels;
using NET1814_MilkShop.Repositories.Models.OrderModels;
using NET1814_MilkShop.Repositories.Models.PaymentModels;
using NET1814_MilkShop.Repositories.Repositories.Interfaces;
using NET1814_MilkShop.Repositories.UnitOfWork.Interfaces;
using NET1814_MilkShop.Services.CoreHelpers.Extensions;
using NET1814_MilkShop.Services.Services.Interfaces;
using Newtonsoft.Json;

namespace NET1814_MilkShop.Services.Services.Implementations;

public class CheckoutService : ICheckoutService
{
    private readonly ICartRepository _cartRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentService _paymentService;
    private readonly IPreorderProductRepository _preorderProductRepository;
    private readonly IEmailService _emailService;
    private readonly IUserRepository _userRepository;
    private readonly ICartDetailRepository _cartDetailRepository;
    private readonly IOrderLogRepository _orderLogRepository;
    private readonly IVoucherRepository _voucherRepository;


    public CheckoutService(
        IUnitOfWork unitOfWork,
        IOrderRepository orderRepository,
        IProductRepository productRepository,
        ICartRepository cartRepository,
        ICustomerRepository customerRepository,
        IPaymentService paymentService,
        IPreorderProductRepository preorderProductRepository,
        IEmailService emailService,
        IUserRepository userRepository,
        ICartDetailRepository cartDetailRepository,
        IOrderLogRepository orderLogRepository,
        IVoucherRepository voucherRepository)
    {
        _customerRepository = customerRepository;
        _cartRepository = cartRepository;
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
        _paymentService = paymentService;
        _preorderProductRepository = preorderProductRepository;
        _emailService = emailService;
        _userRepository = userRepository;
        _cartDetailRepository = cartDetailRepository;
        _orderLogRepository = orderLogRepository;
        _voucherRepository = voucherRepository;
    }

    /// <summary>
    /// checkout cart
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="model"></param>
    /// <returns></returns>
    public async Task<ResponseModel> Checkout(Guid userId, CheckoutModel model)
    {
        // var userActive = await _userRepository.GetByIdAsync(userId);
        var customer = await _customerRepository.GetCustomersQuery()
            .Include(c => c.User).FirstOrDefaultAsync(c => c.UserId == userId);
        if (customer!.User.IsBanned)
        {
            return ResponseModel.BadRequest(ResponseConstants.Banned);
        }

        if (!customer.User.IsActive)
        {
            return ResponseModel.BadRequest(ResponseConstants.UserNotActive);
        }

        var cart = await _cartRepository.GetCartByCustomerId(userId);
        if (cart == null)
        {
            return ResponseModel.BadRequest(ResponseConstants.NotFound("Giỏ hàng"));
        }

        if (!cart.CartDetails.Any())
        {
            return ResponseModel.Success(ResponseConstants.CartIsEmpty, cart.CartDetails);
        }

        //check quantity coi còn hàng không
        List<CheckoutQuantityResponseModel> unavailableItems = [];
        foreach (var c in cart.CartDetails)
        {
            if (c.Quantity > c.Product.Quantity)
            {
                unavailableItems.Add(new CheckoutQuantityResponseModel
                {
                    ProductName = c.Product.Name,
                    Quantity = c.Quantity,
                    Message =
                        $"Số lượng sản phẩm bạn mua ({c.Quantity}) đã vượt quá số lượng sản phẩm còn lại của cửa hàng ({c.Product.Quantity}). Vui lòng kiểm tra lại giỏ hàng của quý khách!"
                });
            }
            else if (c.Product.StatusId is (int)ProductStatusId.OutOfStock or (int)ProductStatusId.Preordered)
            {
                unavailableItems.Add(new CheckoutQuantityResponseModel
                {
                    ProductName = c.Product.Name,
                    Quantity = c.Quantity,
                    Message =
                        "Sản phẩm đã hết hàng hoặc đang trong quá trình pre-order. Vui lòng kiểm tra lại giỏ hàng của quý khách!"
                });
            }
        }

        if (unavailableItems.Count != 0)
        {
            return ResponseModel.BadRequest(ResponseConstants.OverLimit("Số lượng sản phẩm"), unavailableItems);
        }

        // lấy address theo address id
        var customerAddress = await _customerRepository.GetCustomerAddressById(model.AddressId);
        if (customerAddress == null || customerAddress.UserId != userId)
        {
            return ResponseModel.BadRequest(ResponseConstants.NotFound("Địa chỉ"));
        }

        var voucherDiscount = 0;
        // handle voucher
        var totalPrice = GetTotalPrice(cart.CartDetails.ToList());
        var totalPriceBeforeDiscount = totalPrice;
        if (model.VoucherId != Guid.Empty)
        {
            var voucher = await _voucherRepository.GetByIdAsync(model.VoucherId);
            if (voucher == null)
            {
                return ResponseModel.BadRequest(ResponseConstants.NotFound("Voucher"));
            }

            var handleVoucher = DiscountExtension.ApplyVoucher(voucher, totalPrice);
            if (handleVoucher.StatusCode != 200) return handleVoucher;
            voucherDiscount = (int)handleVoucher.Data!;
            totalPrice -= voucherDiscount;
            // update voucher quantity
            voucher.Quantity--;
            _voucherRepository.Update(voucher);
        }

        // handle point
        var pointDiscount = 0;
        if (model.IsUsingPoint)
        {
            var handlePoint = DiscountExtension.ApplyPoint(customer, totalPrice);
            if (handlePoint.StatusCode != 200) return handlePoint;
            pointDiscount = (int)handlePoint.Data!;
            totalPrice -= pointDiscount;
            // trừ xu
            customer.Point -= pointDiscount;
            _customerRepository.Update(customer);
        }

        // thêm vào order
        var orders = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = userId,
            TotalPrice = totalPrice,
            ShippingFee = model.ShippingFee,
            TotalAmount = totalPrice + model.ShippingFee,
            ReceiverName = customerAddress.ReceiverName ?? "",
            Address =
                customerAddress.Address
                + ", "
                + customerAddress.WardName
                + ", "
                + customerAddress.DistrictName
                + ", "
                + customerAddress.ProvinceName,
            WardCode = customerAddress.WardCode,
            DistrictId = customerAddress.DistrictId,
            PhoneNumber =
                customerAddress.PhoneNumber + "", //cộng thêm này để chắc chắn ko null (ko báo lỗi biên dịch)
            Note = model.Note,
            PaymentMethod = model.PaymentMethod,
            StatusId = (int)OrderStatusId.Pending,
            TransactionCode = model.PaymentMethod == "COD" ? null : await GenerateOrderCode(),
            TotalGram = GetTotalGram(cart.CartDetails.ToList()),
            Email = customer.Email,
            VoucherAmount = voucherDiscount,
            PointAmount = pointDiscount,
            IsPreorder = false // set is preorder = false cho order thông thường
        };
        var orderLog = new OrderLog
        {
            OrderId = orders.Id,
            NewStatusId = (int)OrderStatusId.Pending,
            StatusName = OrderStatusId.Pending.ToString(),
        };
        _orderRepository.Add(orders);
        _orderLogRepository.Add(orderLog);
        //thêm vào order detail
        var orderDetailsList = cart.CartDetails.Select(x => new OrderDetail
        {
            OrderId = orders.Id,
            ProductId = x.ProductId,
            Quantity = x.Quantity,
            UnitPrice = x.Product.SalePrice == 0 ? x.Product.OriginalPrice : x.Product.SalePrice,
            ProductName = x.Product.Name,
            ItemPrice =
                x.Quantity
                * (x.Product.SalePrice == 0
                    ? x.Product.OriginalPrice
                    : x.Product.SalePrice), //check sale price va original price
            Thumbnail = x.Product.Thumbnail
        });
        var cartTemp = cart.CartDetails.ToList();
        _orderRepository.AddRange(orderDetailsList);
        // xóa cart detail

        _cartDetailRepository.RemoveRange(cart.CartDetails); ////tạo hàm mẫu ở order repo

        // cập nhật quantity trong product
        foreach (var c in cart.CartDetails)
        {
            c.Product.Quantity -= c.Quantity;
            if (c.Product.Quantity == 0)
            {
                c.Product.StatusId = (int)ProductStatusId.OutOfStock;
            }

            // fetch product from db to check concurrency
            // var currentProduct = await _productRepository.GetByIdNoIncludeAsync(c.ProductId);
            // if (currentProduct == null) return ResponseModel.BadRequest(ResponseConstants.NotFound("Sản phẩm"));
            // if (currentProduct.ModifiedAt != c.Product.ModifiedAt)
            //     return ResponseModel.BadRequest(ResponseConstants.ConcurrencyError);
            _productRepository.Update(c.Product);
        }

        var res = await _unitOfWork.SaveChangesAsync();
        if (res > 0)
        {
            var resp = new CheckoutResponseModel
            {
                OrderId = orders.Id,
                CustomerId = orders.CustomerId,
                FullName = orders.ReceiverName,
                Email = customer.Email,
                TotalPriceBeforeDiscount = totalPriceBeforeDiscount, // Tổng tiền trước khi giảm giá 
                TotalPriceAfterDiscount = orders.TotalPrice, // Tổng tiền sau khi giảm giá
                ShippingFee = orders.ShippingFee,
                TotalAmount = orders.TotalAmount, // Tổng tiền sau khi giảm giá + ship
                TotalGram = orders.TotalGram,
                Address = orders.Address,
                PhoneNumber = orders.PhoneNumber,
                Note = orders.Note,
                PaymentMethod = orders.PaymentMethod,
                CreatedAt = orders.CreatedAt,
                OrderDetail = ToOrderDetailModel(cartTemp),
                Message = "Bạn sẽ nhận được " + orders.TotalPrice.ApplyPercentage(1) +
                          " xu cho đơn hàng này khi đơn hàng được giao thành công!",
                VoucherId = model.VoucherId,
                IsUsingPoint = model.IsUsingPoint,
                VoucherDiscount = voucherDiscount,
                PointDiscount = pointDiscount,
                IsPreorder = orders.IsPreorder
            };
            if (model.PaymentMethod == "PAYOS")
            {
                var paymentLink = await _paymentService.CreatePaymentLink(orders.TransactionCode!.Value);
                if (paymentLink.Status == "Error")
                {
                    return ResponseModel.Error(ResponseConstants.Create("đơn hàng", false));
                }

                var json = JsonConvert.SerializeObject(paymentLink.Data);
                var paymentData = JsonConvert.DeserializeObject<PaymentDataModel>(json);
                resp.OrderCode = paymentData!.OrderCode;
                resp.CheckoutUrl = paymentData.CheckoutUrl;
            }

            await _emailService.SendPurchaseEmailAsync(customer.Email!, customer.User.FirstName!);
            return ResponseModel.Success(ResponseConstants.Create("đơn hàng", true), resp);
        }

        return ResponseModel.Error(ResponseConstants.Create("đơn hàng", false));
    }

    /// <summary>
    /// checkout preorder
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="model"></param>
    /// <returns></returns>
    public async Task<ResponseModel> PreOrderCheckout(Guid userId, PreorderCheckoutModel model)
    {
        var userActive = await _userRepository.GetByIdAsync(userId);
        if (userActive!.IsBanned)
        {
            return ResponseModel.BadRequest(ResponseConstants.Banned);
        }

        if (!userActive.IsActive)
        {
            return ResponseModel.BadRequest(ResponseConstants.UserNotActive);
        }

        var product = await _preorderProductRepository.GetByProductIdAsync(model.ProductId);
        if (product == null || product.Product.StatusId != (int)ProductStatusId.Preordered)
        {
            return ResponseModel.BadRequest(ResponseConstants.NotFound("Sản phẩm") +
                                            " hoặc sản phẩm đang không trong quá trình Pre-order");
        }

        if (DateTime.UtcNow < product.StartDate ||
            DateTime.UtcNow > product.EndDate)
        {
            return ResponseModel.BadRequest(ResponseConstants.NotInPreOrder);
        }
        // nếu số lượng sản phẩm mua + số lượng đã đặt > số lượng sản phẩm tối đa cho phép
        if (model.Quantity + product.Product.Quantity > product.MaxPreOrderQuantity)
        {
            var resp = new CheckoutQuantityResponseModel
            {
                ProductName = product.Product.Name,
                Quantity = model.Quantity,
                Message =
                    $"Số lượng sản phẩm bạn mua ({model.Quantity}) đã vượt quá " +
                    $"số lượng sản phẩm tối đa cho phép ({product.MaxPreOrderQuantity - product.Product.Quantity})."
            };
            return ResponseModel.BadRequest(ResponseConstants.OverLimit("Số lượng sản phẩm"), resp);
        }

        var customerAddress = await _customerRepository.GetCustomerAddressById(model.AddressId);
        if (customerAddress == null || customerAddress.UserId != userId)
        {
            return ResponseModel.BadRequest(ResponseConstants.NotFound("Địa chỉ"));
        }

        var customerEmail = await _customerRepository.GetCustomerEmail(userId);
        var preOrder = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = userId,
            TotalPrice = product.Product.SalePrice == 0
                ? (product.Product.OriginalPrice * model.Quantity)
                : (product.Product.SalePrice * model.Quantity),
            ShippingFee = model.ShippingFee,
            TotalAmount = product.Product.SalePrice == 0
                ? (product.Product.OriginalPrice * model.Quantity) + model.ShippingFee
                : (product.Product.SalePrice * model.Quantity) + model.ShippingFee,
            ReceiverName = customerAddress.ReceiverName + "",
            Address =
                customerAddress.Address
                + ", "
                + customerAddress.WardName
                + ", "
                + customerAddress.DistrictName
                + ", "
                + customerAddress.ProvinceName,
            WardCode = customerAddress.WardCode,
            DistrictId = customerAddress.DistrictId,
            PhoneNumber = customerAddress.PhoneNumber + "", //cộng thêm này để chắc chắn ko null (ko báo lỗi biên dịch)
            Note = model.Note,
            PaymentMethod = "PAYOS",
            StatusId = (int)OrderStatusId.Pending,
            TransactionCode = await GenerateOrderCode(),
            TotalGram = product.Product.Unit!.Gram * model.Quantity,
            Email = customerEmail,
            IsPreorder = true // set is preorder = true
        };
        var orderLog = new OrderLog
        {
            OrderId = preOrder.Id,
            NewStatusId = (int)OrderStatusId.Pending,
            StatusName = OrderStatusId.Pending.ToString(),
        };
        _orderRepository.Add(preOrder);
        _orderLogRepository.Add(orderLog);
        var preOrderDetail = new OrderDetail
        {
            OrderId = preOrder.Id,
            ProductId = product.ProductId,
            Quantity = model.Quantity,
            UnitPrice = product.Product.SalePrice == 0 ? product.Product.OriginalPrice : product.Product.SalePrice,
            ProductName = product.Product.Name,
            ItemPrice =
                model.Quantity
                * (product.Product.SalePrice == 0
                    ? product.Product.OriginalPrice
                    : product.Product.SalePrice), //check sale price va original price
            Thumbnail = product.Product.Thumbnail
        };
        _orderRepository.Add(preOrderDetail);
        product.Product.Quantity += model.Quantity;
        _productRepository.Update(product.Product);
        var res = await _unitOfWork.SaveChangesAsync();
        if (res > 0)
        {
            var resp = new CheckoutResponseModel
            {
                OrderId = preOrder.Id,
                CustomerId = preOrder.CustomerId,
                FullName = preOrder.ReceiverName,
                Email = customerEmail,
                TotalPriceBeforeDiscount = preOrder.TotalPrice, // Tổng tiền trước khi giảm giá
                TotalPriceAfterDiscount =
                    preOrder.TotalPrice, // Tổng tiền sau khi giảm giá   (preorder không áp dụng point + voucher)
                ShippingFee = preOrder.ShippingFee,
                TotalAmount = preOrder.TotalAmount,
                TotalGram = preOrder.TotalGram,
                Address = preOrder.Address,
                PhoneNumber = preOrder.PhoneNumber,
                Note = preOrder.Note,
                PaymentMethod = preOrder.PaymentMethod,
                CreatedAt = preOrder.CreatedAt,
                OrderDetail = new CheckoutOrderDetailModel
                {
                    ProductId = preOrderDetail.ProductId,
                    ProductName = preOrderDetail.ProductName,
                    Quantity = preOrderDetail.Quantity,
                    UnitPrice = preOrderDetail.UnitPrice,
                    ItemPrice = preOrderDetail.ItemPrice,
                    Thumbnail = preOrderDetail.Product.Thumbnail
                },
                Message = "Bạn sẽ nhận được " + Math.Round(preOrder.TotalAmount * 0.01) +
                          " xu cho đơn hàng này!",
                IsPreorder = preOrder.IsPreorder
            };
            var paymentLink = await _paymentService.CreatePaymentLink(preOrder.TransactionCode.Value);
            if (paymentLink.Status == "Error")
            {
                return ResponseModel.Error(ResponseConstants.Create("đơn hàng", false));
            }

            var json = JsonConvert.SerializeObject(paymentLink.Data);
            var paymentData = JsonConvert.DeserializeObject<PaymentDataModel>(json);
            resp.OrderCode = paymentData!.OrderCode;
            resp.CheckoutUrl = paymentData.CheckoutUrl;
            await _emailService.SendPurchaseEmailAsync(customerEmail!, userActive.FirstName!);
            return ResponseModel.Success(ResponseConstants.Create("đơn hàng", true), resp);
        }

        return ResponseModel.Error(ResponseConstants.Create("đơn hàng", false));
    }

    private async Task<int> GenerateOrderCode()
    {
        Random random = new Random();
        int orderCode;
        do
        {
            orderCode = random.Next(0, 10000000);
        } while (await _orderRepository.IsExistOrderCode(orderCode));

        return orderCode;
    }

    private int GetTotalPrice(List<CartDetail> list)
    {
        int total = 0;
        foreach (var x in list)
        {
            var price = x.Product.SalePrice == 0 ? x.Product.OriginalPrice : x.Product.SalePrice;
            total += x.Quantity * price;
        }

        return total;
    }

    private int GetTotalGram(List<CartDetail> list)
    {
        var totalGram = 0;
        foreach (var x in list)
        {
            var gram = x.Product.Unit!.Gram;
            totalGram += x.Quantity * gram;
        }

        return totalGram;
    }

    private IEnumerable<CheckoutOrderDetailModel> ToOrderDetailModel(List<CartDetail> list)
    {
        var res = list.Select(x => new CheckoutOrderDetailModel
        {
            ProductId = x.ProductId,
            ProductName = x.Product.Name,
            Quantity = x.Quantity,
            UnitPrice = x.Product.SalePrice == 0 ? x.Product.OriginalPrice : x.Product.SalePrice,
            ItemPrice =
                x.Quantity
                * (x.Product.SalePrice == 0 ? x.Product.OriginalPrice : x.Product.SalePrice),
            Thumbnail = x.Product.Thumbnail
        });
        return res;
    }
}