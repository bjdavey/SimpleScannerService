using Saraff.Twain;
using Saraff.Twain.Aux;
using Image = System.Drawing.Image;

namespace SimpleScannerService
{
    public class AuxService
    {
        private const string x86Aux = "SimpleScannerService.AUX_x86.exe";
        private const string msilAux = "SimpleScannerService.AUX_MSIL.exe";

        public static string AssemblyLocation()
        {
            return AppDomain.CurrentDomain.BaseDirectory;
            //return Path.GetDirectoryName(this.GetType().Assembly.Location) ?? "";
        }

        public static string AuxX86Path()
        {
            return Path.Combine(AssemblyLocation(), AuxService.x86Aux);
        }
        public static string AuxMSILPath()
        {
            return Path.Combine(AssemblyLocation(), AuxService.msilAux);
        }

        public static AuxResponse<SourceDetails> GetSourceDetails(ITwain32 twain, string sourceName, string auxName)
        {
            var auxResponse = new AuxResponse<SourceDetails>();
            auxResponse.SourceName = sourceName;
            try
            {
                for (var i = 0; i < twain.SourcesCount; i++)
                {
                    if (sourceName == twain.GetSourceProductName(i))
                    {
                        twain.SourceIndex = i;
                        auxResponse.ScannerFound = true;
                        break;
                    }
                }

                if (!auxResponse.ScannerFound)
                    return auxResponse;

                twain.OpenDataSource();

                var pixelTypesEnum = twain.Capabilities.PixelType.Get();
                var pixelTypes = new List<string>();
                for (int x = 0; x < pixelTypesEnum.Count; x++)
                {
                    pixelTypes.Add(pixelTypesEnum[x].ToString() ?? "");
                }

                var supportedSizesEnum = twain.Capabilities.SupportedSizes.Get();
                var supportedSizes = new List<string>();
                for (int x = 0; x < supportedSizesEnum.Count; x++)
                {
                    supportedSizes.Add(supportedSizesEnum[x].ToString() ?? "");
                }

                var xResolutionEnum = twain.Capabilities.XResolution.Get();
                var xResolutions = new List<int>();
                for (int x = 0; x < xResolutionEnum.Count; x++)
                {
                    xResolutions.Add(Convert.ToInt32(xResolutionEnum[x]));
                }

                auxResponse.Response = new SourceDetails()
                {
                    SupportDuplex = twain.Capabilities.DuplexEnabled.GetDefault(),
                    SupportedColors = pixelTypes.ToArray(),
                    SupportedPaperSizes = supportedSizes.ToArray(),
                    SupportedResolutions = xResolutions.Where(dpi => (dpi % 50) == 0).ToArray(),
                };

            }
            catch (Saraff.Twain.TwainException ex)
            {
                if (ex.ConditionCode == Saraff.Twain.TwCC.CheckDeviceOnline)
                    auxResponse.ExceptionOpenScanner = true;
                else
                    auxResponse.AuxExceptionsList.Add($"{auxName}: {ex.Message} - {ex.InnerException?.Message}");
            }
            catch (Exception ex)
            {
                auxResponse.AuxExceptionsList.Add($"{auxName}: {ex.Message} - {ex.InnerException?.Message}");
            }

            return auxResponse;
        }

        public static AuxResponse<List<Image>> GetScannedImages(ITwain32 twain, string auxName, ScanningParameters scanningParameters)
        {
            var auxResponse = new AuxResponse<List<Image>>();
            auxResponse.Response = new List<Image>();
            try
            {
                //Set source (default if not specified)
                if (!string.IsNullOrEmpty(scanningParameters.Source))
                {
                    auxResponse.SourceName = scanningParameters.Source.Trim();
                    for (var i = 0; i < twain.SourcesCount; i++)
                    {
                        if (scanningParameters.Source.Trim() == twain.GetSourceProductName(i))
                        {
                            twain.SourceIndex = i;
                            auxResponse.ScannerFound = true;
                            break;
                        }
                    }
                }
                else
                {
                    auxResponse.SourceName = twain.GetSourceProductName(0);
                    auxResponse.ScannerFound = true;
                }

                if (!auxResponse.ScannerFound)
                    return auxResponse;

                twain.OpenDataSource();


                try
                {
                    if (scanningParameters.DuplexEnabled.HasValue)
                    {
                        twain.Capabilities.DuplexEnabled.Set((bool)scanningParameters.DuplexEnabled);
                    }

                    if (!string.IsNullOrEmpty(scanningParameters.Color))
                    {
                        TwPixelType twPixelType;
                        if (Enum.TryParse(scanningParameters.Color, out twPixelType))
                            twain.Capabilities.PixelType.Set(twPixelType);
                    }

                    if (!string.IsNullOrEmpty(scanningParameters.PaperSize))
                    {
                        TwSS twSupportedSize;
                        if (Enum.TryParse(scanningParameters.PaperSize, out twSupportedSize))
                            twain.Capabilities.SupportedSizes.Set(twSupportedSize);
                    }

                    if (scanningParameters.Resolution.HasValue)
                    {
                        twain.Capabilities.XResolution.Set((int)scanningParameters.Resolution);
                    }

                    if (scanningParameters.RemoveBlankPages.HasValue)
                    {
                        twain.Capabilities.AutoDiscardBlankPages.Set(TwBP.Auto);
                        //if (twain.Capabilities.AutoDiscardBlankPages.IsSupported(TwQC.Set))
                        //{
                        //    twain.Capabilities.AutoDiscardBlankPages.Set(TwBP.Auto);
                        //}
                    }

                }
                catch (TwainException ex)
                {
                    auxResponse.InvalidScannerParameter = true;
                    return auxResponse;
                    //if (ex.ConditionCode == TwCC.BadValue)
                    //{
                    //    auxResponse.InvalidScannerParameter = true;
                    //    return auxResponse;
                    //}
                }



                twain.Capabilities.XferMech.Set(TwSX.File);
                twain.SetupFileXferEvent += (sender, e) =>
                {
                    var fileName = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
                    var fileExt = twain.Capabilities.ImageFileFormat.GetCurrent().ToString().ToLower();
                    e.FileName = Path.Combine(Helpers.TempFolder(), $"{fileName}.{fileExt}");
                };
                twain.FileXferEvent += (sender, e) =>
                {
                    var img = new Bitmap(e.ImageFileXfer.FileName);
                    auxResponse.Response.Add(img);
                };
                twain.AcquireError += (sender, e) =>
                {
                    throw e.Exception;
                };

                twain.Acquire();
            }
            catch (Saraff.Twain.TwainException ex)
            {
                if (ex.ConditionCode == Saraff.Twain.TwCC.CheckDeviceOnline)
                    auxResponse.ExceptionOpenScanner = true;
                else
                    auxResponse.AuxExceptionsList.Add($"{auxName}: {ex.Message} - {ex.InnerException?.Message}");
            }
            catch (Exception ex)
            {
                auxResponse.AuxExceptionsList.Add($"{auxName}: {ex.Message} - {ex.InnerException?.Message}");
            }

            return auxResponse;
        }

    }
}
