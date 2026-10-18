namespace Inv_Property_Tab.Services
{
    public sealed class PropertyWriteResult
    {
        private PropertyWriteResult(bool success, string error)
        {
            Success = success;
            Error = error;
        }

        public bool Success { get; }
        public string Error { get; }

        public static PropertyWriteResult Succeeded()
        {
            return new PropertyWriteResult(true, string.Empty);
        }

        public static PropertyWriteResult Failed(string error)
        {
            return new PropertyWriteResult(false, error ?? string.Empty);
        }
    }
}
