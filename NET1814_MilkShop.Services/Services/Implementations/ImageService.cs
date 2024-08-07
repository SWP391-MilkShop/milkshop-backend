﻿using Microsoft.Extensions.Configuration;
using NET1814_MilkShop.Repositories.CoreHelpers.Constants;
using NET1814_MilkShop.Repositories.Models;
using NET1814_MilkShop.Repositories.Models.ImageModels;
using NET1814_MilkShop.Services.Services.Interfaces;
using Newtonsoft.Json;

namespace NET1814_MilkShop.Services.Services.Implementations;

public class ImageService : IImageService
{
    private readonly string _clientId;
    private readonly string _apiUrl;

    public ImageService(IConfiguration configuration)
    {
        _clientId = configuration["Imgur:ClientId"]!;
        _apiUrl = configuration["Imgur:ApiUrl"]!;
    }

    public async Task<ResponseModel> GetImageAsync(string imageHash)
    {
        using var client = new HttpClient();

        // Add the client_id to the Authorization header
        client.DefaultRequestHeaders.Add("Authorization", "Client-ID " + _clientId);

        // Send the GET request to the Imgur API
        var response = await client.GetAsync($"https://api.imgur.com/3/image/{imageHash}");

        // Read the response content
        var responseContent = await response.Content.ReadAsStringAsync();

        // Deserialize the Imgur API response
        var imgurResponse = JsonConvert.DeserializeObject<ImgurResponse>(responseContent);

        // Return the ResponseModel as a JSON response
        if (imgurResponse != null)
            switch (imgurResponse.Status)
            {
                case 200:
                    return ResponseModel.Success(
                        "Tải thông tin hình ảnh thành công",
                        imgurResponse.Data
                    );
                case 400:
                    return ResponseModel.BadRequest("Yêu cầu không hợp lệ");
                case 404:
                    return ResponseModel.NotFound(ResponseConstants.NotFound("Đường dẫn"));
                default:
                    return ResponseModel.Error("Đã xảy ra lỗi khi tải thông tin hình ảnh");
            }

        return ResponseModel.Error("Đã xảy ra lỗi khi tải thông tin hình ảnh");
    }

    public async Task<ResponseModel> UploadImageAsync(ImageUploadModel model)
    {
        using var client = new HttpClient();

        // Add the client_id to the Authorization header
        client.DefaultRequestHeaders.Add("Authorization", "Client-ID " + _clientId);

        // Create a new MultipartFormDataContent to hold the form data
        using var formData = new MultipartFormDataContent();

        // Read the image into a byte array
        byte[] imageBytes;
        using (var br = new BinaryReader(model.Image.OpenReadStream()))
        {
            imageBytes = br.ReadBytes((int)model.Image.OpenReadStream().Length);
        }

        // Add the image, title, and description to the form data
        formData.Add(new ByteArrayContent(imageBytes, 0, imageBytes.Length), "image");
        formData.Add(new StringContent(model.Title ?? ""), "title");
        formData.Add(new StringContent(model.Description ?? ""), "description");

        // Send the POST request to the Imgur API
        var response = await client.PostAsync(_apiUrl, formData);

        // Read the response content
        var responseContent = await response.Content.ReadAsStringAsync();
        try
        {
            // Convert the response content to a ResponseModel
            var imgurResponse = JsonConvert.DeserializeObject<ImgurResponse>(responseContent);
            if (imgurResponse == null)
            {
                return ResponseModel.Error("Đã xảy ra lỗi khi đăng tải hình ảnh");
            }

            // Return the ResponseModel as a JSON response
            switch (imgurResponse.Status)
            {
                case 200:
                    return ResponseModel.Success(
                        "Đăng tải hình ảnh thành công",
                        imgurResponse.Data
                    );
                case 400:
                    return ResponseModel.BadRequest("Yêu cầu không hợp lệ");
                case 404:
                    return ResponseModel.NotFound(ResponseConstants.NotFound("Đường dẫn"));
                default:
                    return ResponseModel.Error("Đã xảy ra lỗi khi đăng tải hình ảnh");
            }
        }
        catch (JsonReaderException)
        {
            return ResponseModel.Error("Đã xảy ra lỗi khi đăng tải hình ảnh");
        }
    }
}