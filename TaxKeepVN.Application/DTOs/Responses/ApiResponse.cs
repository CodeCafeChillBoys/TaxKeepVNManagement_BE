namespace TaxKeepVN.Application.DTOs.Responses
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ErrorCode { get; set; }
        public T? Data { get; set; }

        public static ApiResponse<T> Ok(T data, string message = "Thành công")
        {
            return new ApiResponse<T> { Success = true, Message = message, Data = data };
        }

        public static ApiResponse<T> Fail(string errorCode, string message)
        {
            return new ApiResponse<T> { Success = false, ErrorCode = errorCode, Message = message, Data = default };
        }
    }
}
