using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Linq;

namespace TaxKeepVN.Application.DTOs.Responses
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public object? Errors { get; set; }

        public static ApiResponse<T> Ok(T? data, string message = "Thành công")
        {
            return new ApiResponse<T> { Success = true, Message = message, Data = data, Errors = null };
        }

        public static ApiResponse<T> Fail(string errorCode, string message)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                Data = default,
                Errors = new { errorCode }
            };
        }

        public static ApiResponse<T> ValidationFail(object errors, string message = "Dữ liệu đầu vào không hợp lệ.")
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                Data = default,
                Errors = errors
            };
        }

        /// <summary>
        /// Overload cho ModelStateDictionary — tự động format lỗi validation thành mảng rõ ràng.
        /// </summary>
        public static ApiResponse<T> ValidationFail(ModelStateDictionary modelState)
        {
            var errors = modelState
                .Where(x => x.Value != null && x.Value.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                );

            return new ApiResponse<T>
            {
                Success = false,
                Message = "Dữ liệu đầu vào không hợp lệ.",
                Data = default,
                Errors = errors
            };
        }
    }
}
