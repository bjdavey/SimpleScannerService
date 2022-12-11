namespace SimpleScannerService
{
    public class Source
    {

        public int Id { get; set; }

        public string Name { get; set; }

        public string Platform { get; set; }

        public int TwainVersion { get; set; }

        public bool IsDefault { get; set; }
    }

    public class SourceDetails
    {
        public bool? SupportDuplex { get; set; }

        public string[]? SupportedColors { get; set; }

        public string[]? SupportedPaperSizes { get; set; }

        public int[]? SupportedResolutions { get; set; }

    }

}
