namespace SimpleScannerService
{
    public enum FileTypes
    {
        PDF = 1,
        JPEG = 2,
        ZIP = 3,
    }

    public enum Errors
    {
        General = 0,
        NoScannerFound = 1,
        ScannerNotAvailable = 2,
        CouldNotOpenScanner = 3,
        TransferError = 4,
        InvalidFileType = 5,
        NoImagesScanned = 6,
    }
}
