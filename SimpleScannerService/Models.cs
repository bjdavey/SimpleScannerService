namespace SimpleScannerService
{
    public class SourceDetails
    {
        public bool? SupportDuplex { get; set; }

        public string[]? SupportedColors { get; set; }

        public string[]? SupportedPaperSizes { get; set; }

        public int[]? SupportedResolutions { get; set; }
    }

    public class ScanningParameters
    {
        public string? Source { get; set; }
        public bool? DuplexEnabled { get; set; }
        public string? Color { get; set; }
        public string? PaperSize { get; set; }
        public int? Resolution { get; set; }
        public bool? RemoveBlankPages { get; set; }
    }

    public class AuxResponse<T>
    {
        public AuxResponse()
        {
            ExceptionOpenScanner = false;
            InvalidScannerParameter = false;
            ScannerFound = false;
            AuxExceptionsList = new List<string>();
        }
        public string SourceName { get; set; }
        public T? Response { get; set; }
        public bool ExceptionOpenScanner { get; set; }
        public bool InvalidScannerParameter { get; set; }
        public bool ScannerFound { get; set; }
        public List<string> AuxExceptionsList { get; set; }
    }

}
