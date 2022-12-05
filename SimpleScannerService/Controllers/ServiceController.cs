using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NTwain;
using NTwain.Data;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Reflection;
using Image = System.Drawing.Image;

namespace SimpleScannerService.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("ScannerService/[action]")]
    public class ServiceController : ControllerBase
    {

        private TwainSession twainSession;
        public ServiceController()
        {
            NTwain.PlatformInfo.Current.PreferNewDSM = false;
            var appId = TWIdentity.CreateFromAssembly(DataGroups.Image, Assembly.GetExecutingAssembly());
            twainSession = new TwainSession(appId);
        }

        [HttpGet]
        public ActionResult Test()
        {
            try
            {
                return Ok("HelloWorld");
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status406NotAcceptable, new
                {
                    Code = Errors.General,
                    Message = ex.Message + " - " + ex.InnerException?.Message
                });
            }
        }

        [HttpGet]
        public ActionResult GetAvailableScanners()
        {
            try
            {
                twainSession.Open();
                var list = twainSession.GetSources().ToList().Select(source => source.Name).ToArray();
                twainSession.Close();
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status406NotAcceptable, new
                {
                    Code = Errors.General,
                    Message = ex.Message + " - " + ex.InnerException?.Message
                });
            }
        }

        [HttpGet]
        public ActionResult GetScannerDetails([FromQuery] string ScannerName)
        {
            try
            {
                twainSession.Open();

                var scanner = twainSession.GetSources().FirstOrDefault(x => x.Name == ScannerName);

                if (scanner == null)
                {
                    return StatusCode(StatusCodes.Status406NotAcceptable, new
                    {
                        Code = Errors.ScannerNotAvailable,
                        Message = $"'{ScannerName}' scanner is not available"
                    });
                }

                scanner.Open();

                if (!scanner.IsOpen)
                {
                    return StatusCode(StatusCodes.Status406NotAcceptable, new
                    {
                        Code = Errors.CouldNotOpenScanner,
                        Message = $"Could not open the scanner: '{scanner.Name}'"
                    });
                }

                var result = new
                {
                    Name = scanner.Name,
                    SupportDuplex = scanner.Capabilities.CapDuplexEnabled.IsSupported,
                    SupportedColors = scanner.Capabilities.ICapPixelType?.GetValues()?.Select(x => x.ToString()),
                    SupportedPaperSizes = scanner.Capabilities.ICapSupportedSizes?.GetValues()?.Select(x => x.ToString()),
                    SupportedResolutions = scanner.Capabilities.ICapXResolution?.GetValues()?.Where(dpi => (dpi % 50) == 0).Select(x => x.Whole)
                };

                scanner.Close();
                twainSession.Close();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status406NotAcceptable, new
                {
                    Code = Errors.General,
                    Message = ex.Message + " - " + ex.InnerException?.Message
                });
            }
        }


        [HttpGet]
        public ActionResult GetAvailableScannersDetailed()
        {
            try
            {
                twainSession.Open();

                var list = new List<dynamic>();

                twainSession.GetSources().ToList().ForEach(source =>
                {
                    source.Open();
                    if (source.IsOpen)
                    {
                        var item = new
                        {
                            Name = source.Name,
                            SupportDuplex = source.Capabilities.CapDuplexEnabled.IsSupported,
                            SupportedColors = source.Capabilities.ICapPixelType?.GetValues()?.Select(x => x.ToString()),
                            SupportedPaperSizes = source.Capabilities.ICapSupportedSizes?.GetValues()?.Select(x => x.ToString()),
                            SupportedResolutions = source.Capabilities.ICapXResolution?.GetValues()?.Where(dpi => (dpi % 50) == 0).Select(x => x.Whole)
                        };
                        list.Add(item);
                        source.Close();
                    }
                });
                twainSession.Close();
                return Ok(list);

            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status406NotAcceptable, new
                {
                    Code = Errors.General,
                    Message = ex.Message + " - " + ex.InnerException?.Message
                });
            }
        }


        [HttpGet]
        public ActionResult Scan(
            [FromQuery] string? ScannerName,
            [FromQuery] FileTypes? FileType, //JPEG,PDF,ZIP
            [FromQuery] bool? DuplexEnabled,
            [FromQuery] PixelType? Color, //RGB, BlackWhite, Gray
            [FromQuery] SupportedSize? PaperSize, //A4,A3...etc
            [FromQuery] int? Resolution
        )
        {
            try
            {
                twainSession.Open();

                List<Image> scannedImages = new List<Image>();
                twainSession.DataTransferred += (s, e) =>
                {
                    if (e.NativeData != IntPtr.Zero)
                    {
                        var stream = e.GetNativeImageStream();
                        if (stream != null)
                        {
                            scannedImages.Add(Image.FromStream(stream));
                        }
                    }
                };

                var TransferError = false;
                twainSession.TransferError += (s, e) =>
                {
                    TransferError = true;
                };
                if (TransferError)
                {
                    return StatusCode(StatusCodes.Status417ExpectationFailed, new
                    {
                        Code = Errors.TransferError,
                        Message = $"Transfer Error"
                    });
                }

                twainSession.Open();

                DataSource scanner = twainSession.DefaultSource;
                if (scanner == null)
                {
                    return StatusCode(StatusCodes.Status406NotAcceptable, new
                    {
                        Code = Errors.NoScannerFound,
                        Message = $"Could not find any scanner"
                    });
                }

                if (ScannerName != null)
                {
                    var value = twainSession.GetSources().FirstOrDefault(x => x.Name == ScannerName);
                    if (value != null)
                    {
                        scanner = value;
                    }
                    else
                    {
                        return StatusCode(StatusCodes.Status406NotAcceptable, new
                        {
                            Code = Errors.ScannerNotAvailable,
                            Message = $"'{ScannerName}' scanner is not available"
                        });
                    }
                }
                scanner.Open();
                if (!scanner.IsOpen)
                {
                    return StatusCode(StatusCodes.Status406NotAcceptable, new
                    {
                        Code = Errors.CouldNotOpenScanner,
                        Message = $"Could not open the scanner: '{scanner.Name}'"
                    });
                }

                if (scanner.Capabilities.CapDuplexEnabled.IsSupported)
                {
                    if (DuplexEnabled == null) DuplexEnabled = true;
                    var value = (bool)DuplexEnabled ? BoolType.True : BoolType.False;
                    scanner.Capabilities.CapDuplexEnabled.SetValue(value);
                }

                if (scanner.Capabilities.ICapPixelType.IsSupported)
                {
                    if (Color == null) Color = PixelType.RGB;
                    scanner.Capabilities.ICapPixelType.SetValue((PixelType)Color);
                }

                if (PaperSize != null && scanner.Capabilities.ICapSupportedSizes.IsSupported)
                {
                    scanner.Capabilities.ICapSupportedSizes.SetValue((SupportedSize)PaperSize);
                }

                if (scanner.Capabilities.ICapXResolution.IsSupported && scanner.Capabilities.ICapYResolution.IsSupported)
                {
                    if (Resolution == null) Resolution = 200;
                    var value = (TWFix32)Resolution;
                    scanner.Capabilities.ICapXResolution.SetValue(value);
                    scanner.Capabilities.ICapYResolution.SetValue(value);
                }

                // Start Scan
                scanner.Enable(SourceEnableMode.NoUI, false, IntPtr.Zero);
                //scanner.Enable(SourceEnableMode.ShowUI, false, IntPtr.Zero);

                scanner.Close();
                twainSession.Close();

                switch (FileType)
                {
                    case null:
                    case FileTypes.PDF:
                        return File(CreatePdfDocument(scannedImages), "application/pdf");
                    case FileTypes.JPEG:
                        return File(ToStream(scannedImages.FirstOrDefault()), "image/jpeg");
                    case FileTypes.ZIP:
                        return File(CreateZipCollection(scannedImages), "application/zip");
                    default:
                        return StatusCode(StatusCodes.Status406NotAcceptable, new
                        {
                            Code = Errors.InvalidFileType,
                            Message = $"Invalid file type: '{FileType}'"
                        });
                }

            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status406NotAcceptable, new
                {
                    Code = Errors.General,
                    Message = ex.Message + " - " + ex.InnerException?.Message
                });
            }
        }

        private Stream ToStream(Image image)
        {
            var stream = new System.IO.MemoryStream();
            image.Save(stream, ImageFormat.Jpeg);
            stream.Position = 0;
            return stream;
        }

        private byte[] CreateZipCollection(List<Image> images)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    for (var i = 0; i < images.Count; i++)
                    {
                        var img = images[i];
                        var name = $"{i + 1}.jpeg";
                        var entry = archive.CreateEntry(name);
                        using (var entryStream = entry.Open())
                        {
                            ToStream(img).CopyTo(entryStream);
                        }
                    }
                }
                ms.Position = 0;
                return ms.ToArray(); ;
            }
        }

        private static byte[] CreatePdfDocument(List<Image> images)
        {
            byte[] pdfBytes;
            using (MemoryStream ms = new MemoryStream())
            {
                Document doc = new Document(PageSize.A4);
                PdfWriter writer = PdfWriter.GetInstance(doc, ms);
                doc.Open();
                images.ForEach(image =>
                {
                    doc.NewPage();
                    iTextSharp.text.Image img = iTextSharp.text.Image.GetInstance(image, ImageFormat.Jpeg);
                    img.Alignment = Element.ALIGN_CENTER;
                    img.ScaleToFit(doc.PageSize.Width - 10, doc.PageSize.Height - 10);
                    doc.Add(img);
                });
                doc.Close();
                pdfBytes = ms.ToArray();
            }
            return pdfBytes;
        }


    }
}