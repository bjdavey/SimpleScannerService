using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saraff.Twain.Aux;

namespace SimpleScannerService.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("ScannerService/[action]")]
    public class ServiceController : ControllerBase
    {

        public ServiceController()
        {

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
        public ActionResult GetAvailableScanners([FromQuery] bool TwainV2Support = true)
        {
            try
            {
                List<string> list = new List<string>();
                List<string> auxExceptionsList = new List<string>();

                #region AuxMSIL
                if (Environment.Is64BitOperatingSystem && TwainV2Support)
                {
                    TwainExternalProcess.Execute(AuxService.AuxMSILPath(),
                        twain =>
                        {
                            if (!twain.IsTwain2Supported) return;
                            for (var i = 0; i < twain.SourcesCount; i++)
                            {
                                list.Add(twain.GetSourceProductName(i));
                            }
                        });
                }
                #endregion

                #region AuxX86
                TwainExternalProcess.Execute(AuxService.AuxX86Path(),
                    twain =>
                    {
                        for (var i = 0; i < twain.SourcesCount; i++)
                        {
                            list.Add(twain.GetSourceProductName(i));
                        }
                    });
                #endregion

                var auxException = string.Join(" + ", auxExceptionsList);
                if (!string.IsNullOrEmpty(auxException))
                {
                    throw new Exception(auxException);
                }

                return Ok(list.Distinct().ToArray());
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
        public ActionResult GetScannerDetails(
            [FromQuery] string ScannerName,
            [FromQuery] bool TwainV2Support = true
        )
        {
            try
            {
                AuxResponse<SourceDetails> sourceDetails = new AuxResponse<SourceDetails>();

                //Firstly, let's try AuxMSIL
                #region AuxMSIL
                if (Environment.Is64BitOperatingSystem && TwainV2Support)
                {
                    TwainExternalProcess.Execute(AuxService.AuxMSILPath(),
                        twain =>
                        {
                            if (!twain.IsTwain2Supported) return;
                            sourceDetails = AuxService.GetSourceDetails(twain, ScannerName.Trim(), "AuxMSIL");
                        });
                }
                #endregion
                if (sourceDetails.ExceptionOpenScanner)
                {
                    return StatusCode(StatusCodes.Status406NotAcceptable, new
                    {
                        Code = Errors.CouldNotOpenScanner,
                        Message = $"Could not open the scanner: '{ScannerName}'"
                    });
                }

                //Let's try AuxX86Path if we couldn't find the source
                if (!sourceDetails.ScannerFound)
                {
                    #region AuxX86
                    TwainExternalProcess.Execute(AuxService.AuxX86Path(),
                        twain =>
                        {
                            sourceDetails = AuxService.GetSourceDetails(twain, ScannerName.Trim(), "AuxX86");
                        });
                    #endregion
                }
                if (sourceDetails.ExceptionOpenScanner)
                {
                    return StatusCode(StatusCodes.Status406NotAcceptable, new
                    {
                        Code = Errors.CouldNotOpenScanner,
                        Message = $"Could not open the scanner: '{ScannerName}'"
                    });
                }

                var auxException = string.Join(" + ", sourceDetails.AuxExceptionsList);
                if (!string.IsNullOrEmpty(auxException))
                {
                    throw new Exception(auxException);
                }

                if (!sourceDetails.ScannerFound)
                {
                    return StatusCode(StatusCodes.Status406NotAcceptable, new
                    {
                        Code = Errors.ScannerNotAvailable,
                        Message = $"'{ScannerName}' scanner is not available"
                    });
                }

                return Ok(sourceDetails.Response);
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
            [FromQuery] string? Color, //RGB, BlackWhite, Gray
            [FromQuery] string? PaperSize, //A4,A3...etc
            [FromQuery] int? Resolution,
            [FromQuery] bool? RemoveBlankPages,
            [FromQuery] bool TwainV2Support = true
        )
        {
            try
            {
                Helpers.CleanTempFolder();

                AuxResponse<List<Image>> scannedImages = new AuxResponse<List<Image>>();
                var parameters = new ScanningParameters()
                {
                    Source = ScannerName,
                    DuplexEnabled = DuplexEnabled,
                    Color = Color,
                    PaperSize = PaperSize,
                    Resolution = Resolution,
                    RemoveBlankPages = RemoveBlankPages
                };

                //Firstly, let's try AuxMSIL
                #region AuxMSIL
                if (Environment.Is64BitOperatingSystem && TwainV2Support)
                {
                    TwainExternalProcess.Execute(AuxService.AuxMSILPath(),
                        twain =>
                        {
                            if (!twain.IsTwain2Supported) return;
                            scannedImages = AuxService.GetScannedImages(twain, "AuxMSIL", parameters);
                        });
                    if (scannedImages.InvalidScannerParameter)
                    {
                        return StatusCode(StatusCodes.Status406NotAcceptable, new
                        {
                            Code = Errors.InvalidScannerParameter,
                            Message = $"Invalid Scanner Parameter"
                        });
                    }
                    if (scannedImages.ExceptionOpenScanner)
                    {
                        return StatusCode(StatusCodes.Status406NotAcceptable, new
                        {
                            Code = Errors.CouldNotOpenScanner,
                            Message = $"Could not open the scanner: '{scannedImages.SourceName}'"
                        });
                    }
                }
                #endregion

                //Let's try AuxX86Path if we couldn't find the source
                if (!scannedImages.ScannerFound)
                {
                    #region AuxX86
                    TwainExternalProcess.Execute(AuxService.AuxX86Path(),
                        twain =>
                        {
                            scannedImages = AuxService.GetScannedImages(twain, "AuxX86", parameters);
                        });
                    if (scannedImages.InvalidScannerParameter)
                    {
                        return StatusCode(StatusCodes.Status406NotAcceptable, new
                        {
                            Code = Errors.InvalidScannerParameter,
                            Message = $"Invalid Scanner Parameter"
                        });
                    }
                    if (scannedImages.ExceptionOpenScanner)
                    {
                        return StatusCode(StatusCodes.Status406NotAcceptable, new
                        {
                            Code = Errors.CouldNotOpenScanner,
                            Message = $"Could not open the scanner: '{scannedImages.SourceName}'"
                        });
                    }
                    #endregion
                }

                var auxException = string.Join(" + ", scannedImages.AuxExceptionsList);
                if (!string.IsNullOrEmpty(auxException))
                {
                    throw new Exception(auxException);
                }

                if (!scannedImages.ScannerFound)
                {
                    if (string.IsNullOrEmpty(ScannerName))
                    {
                        return StatusCode(StatusCodes.Status406NotAcceptable, new
                        {
                            Code = Errors.NoScannerFound,
                            Message = $"Could not find any scanner"
                        });
                    }
                    else
                    {
                        return StatusCode(StatusCodes.Status406NotAcceptable, new
                        {
                            Code = Errors.ScannerNotAvailable,
                            Message = $"'{scannedImages.SourceName}' scanner is not available"
                        });
                    }
                }

                if (scannedImages.Response == null || scannedImages.Response.Count == 0)
                {
                    return StatusCode(StatusCodes.Status406NotAcceptable, new
                    {
                        Code = Errors.NoImagesScanned,
                        Message = $"No images could be scanned"
                    });
                }

                switch (FileType)
                {
                    case null:
                    case FileTypes.PDF:
                        var pdf = Helpers.CreatePdfDocument(scannedImages.Response);
                        return File(pdf, "application/pdf");
                    case FileTypes.JPEG:
                        var jpeg = Helpers.ToStream(scannedImages.Response.FirstOrDefault());
                        return File(jpeg, "image/jpeg");
                    case FileTypes.ZIP:
                        var zip = Helpers.CreateZipCollection(scannedImages.Response);
                        return File(zip, "application/zip");
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
    }
}