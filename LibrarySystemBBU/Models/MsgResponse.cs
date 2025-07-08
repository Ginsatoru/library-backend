// Models/MsgResponse.cs
namespace LibrarySystemBBU.Models
{
    public class MsgResponse
    {
        public bool IsSuccess { get; set; } // <-- Renamed from 'Status' to 'IsSuccess'
        public string Message { get; set; } // This property remains the same

        // Constructor
        public MsgResponse(bool isSuccess, string message) // <-- Updated constructor parameter name
        {
            IsSuccess = isSuccess; // <-- Assign to IsSuccess
            Message = message;
        }
    }
}