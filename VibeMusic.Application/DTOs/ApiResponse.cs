using System;

namespace YoutubeMusicPlayer.Application.DTOs;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public string? ErrorCode { get; set; }

    public static ApiResponse<T> SuccessResult(T data, string? message = null) => 
        new() { Success = true, Data = data, Message = message };

    public static ApiResponse<T> ErrorResult(string message, string? errorCode = null) => 
        new() { Success = false, Message = message, ErrorCode = errorCode };
}
