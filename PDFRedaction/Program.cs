using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace PDFRedaction
{
    public class PdfMasking
    {
        public static string maskingStartCoordinates = string.Empty;
        public static string maskingEndCoordinates = string.Empty;
        public static bool maskingSuccessfull = false;
        static void Main(string[] args)
        {
            string inputFilePath = @"C:\Users\vinaym\Downloads\Property App 5-30-18 - Live Example (1).pdf";
            string outputDirPath = @"C:\Users\vinaym\Desktop\test-folder\redacted-pdfs";
            string maskingKeyWord = "premiu";
            //string inputFilePath = args[0];
            //string outputDirPath = args[1];

            string redactedPDFNameEscaped = Regex.Replace(Path.GetFileName(inputFilePath), "%", "%%");
            string redactedPDFPath = $@"{outputDirPath}\{redactedPDFNameEscaped}";

            string jpgFilesDir = ConvertPDFToJPGs(inputFilePath);
            var redactedJPGFiles = RedactPremium(jpgFilesDir, maskingKeyWord);
            if (maskingSuccessfull)
            {
                ConvertJPGsToPDF(redactedJPGFiles, redactedPDFPath);
            }
            Directory.Delete(jpgFilesDir, recursive: true);
        }

        private static string ConvertPDFToJPGs(string pdfFilePath)
        {
            string pdfFileDirectory = Path.GetDirectoryName(pdfFilePath);
            string pdfFileName = Path.GetFileName(pdfFilePath);

            Guid guid = Guid.NewGuid();
            string jpegFilesDirName = guid.ToString().Substring(0, 3) + "_" + Path.GetFileNameWithoutExtension(pdfFilePath);
            string jpegFilesDirPath = Path.Combine(pdfFileDirectory, jpegFilesDirName);
            Directory.CreateDirectory(jpegFilesDirPath);

            string magickCommand = @"C:\Program Files\ImageMagick-7.0.7-Q16\magick.exe";
            string magickCommandArgs = $@"convert -limit memory unlimited -density 300 -quality 100 ""{pdfFilePath}"" ""{jpegFilesDirPath}\{jpegFilesDirName}.jpg""";
            Utils.RunCommand(magickCommand, magickCommandArgs, ConfigurationSettings.AppSettings["image_magick_install_dir"]);
            return jpegFilesDirPath;
        }

        private static void ConvertJPGsToPDF(List<String> jpgFilePaths, string redactedPDFPath)
        {
            string spaceSepJPGFiles = jpgFilePaths.Aggregate("", (acc, jpgFilePath) => $@"{acc}""{jpgFilePath}"" ").Trim();

            string magickCommand = @"C:\Program Files\ImageMagick-7.0.7-Q16\magick.exe";
            string magickCommandArgs = $@"convert -limit memory unlimited -interlace Plane -sampling-factor 4:2:0 -quality 70 -density 150x150 -units PixelsPerInch -resize 1241x1754 -repage 1241x1754 {spaceSepJPGFiles} ""{redactedPDFPath}""";
            Utils.RunCommand(magickCommand, magickCommandArgs, ConfigurationSettings.AppSettings["image_magick_install_dir"]);
        }

        private static List<string> RedactPremium(string jpgFilesDir, string maskingKeyWord)
        {
            int jpgFileCounter = 0;
            int loopCount = 0;
            int premiumCount = 0;
            bool premium = false;
            bool currencyFlag = false;
            var jpgFiles = Directory.EnumerateFiles(jpgFilesDir)
                .OrderBy(file => Utils.ParsePageNumber(file))
                .ToList();

            foreach (string jpgFile in jpgFiles)
            {
                jpgFileCounter++;
                if (premiumCount < 2 && jpgFileCounter < 10)
                {
                    string hocrFile = ConvertJPGToHocr(jpgFile);
                    string hocrContent = File.ReadAllText(hocrFile);
                    if (hocrContent.ToLower().Contains(maskingKeyWord.ToLower()))
                    {
                        HtmlDocument matchesHtmlDoc = new HtmlDocument();
                        matchesHtmlDoc.LoadHtml(hocrContent);
                        string currencies = "GBP|CAD|RMB|ALL|DZD|ARS|AWG|AUD|EUR|BHD|BYR|BOB|BAM|BRL|BGN|CLP|CNY|COP|CRC|HRK|CUP|CZK|DKK|DOP|USD|EGP|SVC|GTQ|HNL|HKD|HUF|ISK|INR|IDR|IQD|ILS|JPY|JMD|JOD|KWD|LBP|LYD|MKD|MYR|MXN|MAD|NZD|NIO|NOK|OMR|PAB|PYG|PEN|PHP|PLN|QAR|RON|RUB|SAR|RSD|CSD|SGD|ZAR|KRW|SDG|SEK|CHF|SYP|TWD|THB|TND|NTD|TRY|UAH|AED|UYU|VEF|VND|YER|QTZ|MXP|TTD|NIS";
                        string[] arrCurrencies = currencies.Split('|').ToArray();
                        string spanClass = "ocr_line";
                        HtmlNodeCollection innerSpans = matchesHtmlDoc.DocumentNode.SelectNodes("//span[@class = '" + spanClass + "']/span");
                        int rightYAxis = 0;
                        int rightXAxis = 0;
                        int leftXAxis = 0;
                        for (int i = 0; i < innerSpans.Count; i++)
                        {
                            string divOuterHTML = innerSpans[i].OuterHtml;
                            string startcor = divOuterHTML.Substring(divOuterHTML.IndexOf("bbox") + 5);
                            string startCoordinates = startcor.Substring(0, startcor.IndexOf("; x_wconf"));
                            string[] arrStartCoordinates = startCoordinates.Split(' ').ToArray();
                            if (Convert.ToInt32(arrStartCoordinates[0]) < 420)
                            {
                                if (i == loopCount + 1 && premium)
                                {
                                    string nextDivOuterHTML = innerSpans[i].OuterHtml;
                                    string nxtStartCoor = nextDivOuterHTML.Substring(nextDivOuterHTML.IndexOf("bbox") + 5);
                                    string nextSpanStartCoordinates = nxtStartCoor.Substring(0, nxtStartCoor.IndexOf("; x_wconf"));
                                    string[] arrnextSpanStartCoordinates = nextSpanStartCoordinates.Split(' ').ToArray();
                                    arrnextSpanStartCoordinates[2] = Convert.ToString(Convert.ToInt32(arrnextSpanStartCoordinates[2]) + 1040);

                                    if (innerSpans[i].InnerHtml.ToLower().Contains(maskingKeyWord.ToLower()))
                                    {
                                        premiumCount++;
                                        string[] arrEndCoordinates = maskingEndCoordinates.Split(' ').ToArray();
                                        arrEndCoordinates[3] = Convert.ToString(Convert.ToInt32(arrnextSpanStartCoordinates[3]) - 70);

                                        string[] arrFinalStartCoordinates = maskingStartCoordinates.Split(' ').ToArray();
                                        maskingStartCoordinates = String.Concat(arrFinalStartCoordinates[0], " ",
                                            Convert.ToString(Convert.ToInt32(arrFinalStartCoordinates[1]) - 12), " ",
                                            arrFinalStartCoordinates[2], " ",
                                            arrFinalStartCoordinates[3]);
                                        maskingEndCoordinates = String.Concat(arrEndCoordinates[0], " ",
                                        arrEndCoordinates[1], " ", arrnextSpanStartCoordinates[2], " ",
                                        arrEndCoordinates[3]);
                                    }
                                    else
                                    {
                                        string[] arrEndCoordinates = maskingEndCoordinates.Split(' ').ToArray();
                                        arrEndCoordinates[3] = Convert.ToString(Convert.ToInt32(arrEndCoordinates[3]) + 12);

                                        string[] arrFinalStartCoordinates = maskingStartCoordinates.Split(' ').ToArray();
                                        maskingStartCoordinates = String.Concat(arrFinalStartCoordinates[0], " ",
                                            Convert.ToString(Convert.ToInt32(arrFinalStartCoordinates[1]) - 12), " ",
                                            arrFinalStartCoordinates[2], " ",
                                            arrFinalStartCoordinates[3]);
                                        maskingEndCoordinates = String.Concat(arrEndCoordinates[0], " ",
                                        arrEndCoordinates[1], " ", arrnextSpanStartCoordinates[2], " ",
                                        arrEndCoordinates[3]);
                                    }
                                    currencyFlag = true;
                                    break;
                                }
                                else
                                {
                                    if (innerSpans[i].InnerHtml.ToLower().Contains(maskingKeyWord.ToLower()))
                                    {
                                        premiumCount++;
                                        if (!premium)
                                        {
                                            loopCount = i;
                                            premium = true;
                                            string nextDivOuterHTML = innerSpans[i].OuterHtml;
                                            string nxtStartCoor = nextDivOuterHTML.Substring(nextDivOuterHTML.IndexOf("bbox") + 5);
                                            string nextSpanStartCoordinates = nxtStartCoor.Substring(0, nxtStartCoor.IndexOf("; x_wconf"));
                                            string[] arrnextSpanStartCoordinates = nextSpanStartCoordinates.Split(' ').ToArray();
                                            arrnextSpanStartCoordinates[0] = "600";
                                            arrnextSpanStartCoordinates[1] = Convert.ToString(Convert.ToInt32(arrnextSpanStartCoordinates[1]) - 11);

                                            nextSpanStartCoordinates = String.Concat(arrnextSpanStartCoordinates[0], " ",
                                                arrnextSpanStartCoordinates[1], " ", arrnextSpanStartCoordinates[2], " ",
                                                arrnextSpanStartCoordinates[3]);
                                            if (Convert.ToInt32(arrnextSpanStartCoordinates[0]) > 550)
                                            {
                                                maskingStartCoordinates = nextSpanStartCoordinates;
                                                maskingEndCoordinates = nextSpanStartCoordinates;
                                            }
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        if (premium)
                                        {
                                            break;
                                        }
                                    }
                                }
                            }
                            else if (!string.IsNullOrEmpty(maskingStartCoordinates) && !string.IsNullOrEmpty(maskingEndCoordinates)
                                && premium)
                            {
                                if (Convert.ToInt32(arrStartCoordinates[0]) > 550)
                                {
                                    if (maskingKeyWord.ToLower().Equals("premiu"))
                                    {
                                        if (arrCurrencies.Contains(innerSpans[i].InnerHtml) || innerSpans[i].InnerHtml.Contains("%"))
                                        {
                                            currencyFlag = true;
                                        }
                                    }
                                    else
                                    {
                                        currencyFlag = true;
                                    }
                                    string[] arrEndCoordinates = maskingEndCoordinates.Split(' ').ToArray();
                                    leftXAxis = Convert.ToInt32(arrStartCoordinates[0]);
                                    if (Convert.ToInt32(arrEndCoordinates[2]) < rightXAxis ?
                                        (rightXAxis < Convert.ToInt32(arrStartCoordinates[2])) :
                                         (Convert.ToInt32(arrEndCoordinates[2]) <= Convert.ToInt32(arrStartCoordinates[2])))
                                    {
                                        rightXAxis = Convert.ToInt32(arrStartCoordinates[2]);
                                    }
                                    if (Convert.ToInt32(arrEndCoordinates[3]) < rightYAxis ?
                                        (rightYAxis < Convert.ToInt32(arrStartCoordinates[3])) :
                                        (Convert.ToInt32(arrEndCoordinates[3]) <= Convert.ToInt32(arrStartCoordinates[3])))
                                    {
                                        rightYAxis = Convert.ToInt32(arrStartCoordinates[3]);
                                    }
                                }
                                else
                                {
                                    break;
                                }
                                maskingEndCoordinates = String.Concat(rightXAxis, " ", rightYAxis, " ", rightXAxis, " ", rightYAxis);
                            }
                        }
                        if (!string.IsNullOrEmpty(maskingStartCoordinates) && !string.IsNullOrEmpty(maskingEndCoordinates) &&
                            !maskingStartCoordinates.Equals(maskingEndCoordinates) && currencyFlag)
                        {
                            string[] arrFinalStartCoordinates = maskingStartCoordinates.Split(' ').ToArray();
                            string[] arrFinalEndCoordinates = maskingEndCoordinates.Split(' ').ToArray();
                            Coordinates coordinates = new Coordinates(Convert.ToInt32(arrFinalStartCoordinates[0]),
                                Convert.ToInt32(arrFinalStartCoordinates[1]),
                                Convert.ToInt32(arrFinalEndCoordinates[2]), Convert.ToInt32(arrFinalEndCoordinates[3]));
                            maskingStartCoordinates = string.Empty;
                            maskingEndCoordinates = string.Empty;
                            premium = false;
                            ChangePixelColor(jpgFile, coordinates);
                        }
                    }
                }
                else
                {
                    break;
                }
            }
            return jpgFiles;
        }

        private static void ChangePixelColor(string jpgFile, Coordinates coordinates)
        {
            try
            {
                Bitmap sourceBitmap = (Bitmap)Image.FromFile(jpgFile);
                Bitmap dupBitmap = new Bitmap(sourceBitmap.Width, sourceBitmap.Height, sourceBitmap.PixelFormat);

                using (var gr = Graphics.FromImage(dupBitmap))
                    gr.DrawImage(sourceBitmap, new Rectangle(0, 0, sourceBitmap.Width, sourceBitmap.Height));

                sourceBitmap.Dispose();
                File.Delete(jpgFile);

                for (int i = Convert.ToInt32(coordinates.LeftX); i <= Convert.ToInt32(coordinates.RightX); i++)
                    for (int j = Convert.ToInt32(coordinates.LeftY); j <= Convert.ToInt32(coordinates.RightY); j++)
                        dupBitmap.SetPixel(i, j, Color.White);

                dupBitmap.Save(jpgFile);
                maskingSuccessfull = true;
            }
            catch (Exception exception)
            {
                Console.WriteLine("Exception occured while chaning pixel color");
                Console.WriteLine(exception.StackTrace);
                throw exception;
            }
        }

        private static string ConvertJPGToHocr(string jpegFilePath)
        {
            string hocrFilePath = jpegFilePath.Replace(Path.GetExtension(jpegFilePath), "");

            string tesseractCommand = @"C:\Program Files (x86)\Tesseract-OCR\tesseract.exe";
            string tesseractArgs = $@"""{jpegFilePath}"" ""{hocrFilePath}"" -l eng -psm 1 hocr";
            Utils.RunCommand(tesseractCommand, tesseractArgs, ConfigurationSettings.AppSettings["tesseract_install_dir"]);
            return $"{hocrFilePath}.hocr";
        }
    }

    class Coordinates
    {
        public int LeftX { get; }
        public int LeftY { get; }
        public int RightX { get; }
        public int RightY { get; }

        public Coordinates(int leftX, int leftY, int rightX, int rightY)
        {
            this.LeftX = leftX;
            this.LeftY = leftY;
            this.RightX = rightX;
            this.RightY = rightY;
        }

        public override string ToString()
        {
            return $"{LeftX} {LeftY} {RightX} {RightY}";
        }
    }

    class CooridnatesYComparer : IComparer<Coordinates>
    {
        public int Compare(Coordinates coord1, Coordinates coord2)
        {
            if (Math.Abs(coord1.LeftY - coord2.LeftY) <= 30 || coord1.LeftY == coord2.LeftY)
                return 0;
            else if (coord1.LeftY < coord2.LeftY)
                return -1;
            else
                return 1;
        }
    }

    class CoordinatesXComparer : IComparer<Coordinates>
    {
        public int Compare(Coordinates coord1, Coordinates coord2)
        {
            if (coord1.LeftX < coord2.LeftX)
                return -1;
            else if (coord1.LeftX == coord2.LeftX)
                return 0;
            else
                return 1;
        }
    }

    class Utils
    {
        public static string prem_text = "";

        public static void RunCommand(string Command, string Args, string CommandDir)
        {
            string output = "";
            string error = "";
            try
            {

                ProcessStartInfo startInfo = new ProcessStartInfo(Command, Args);
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                startInfo.CreateNoWindow = true;
                startInfo.UseShellExecute = false;
                //startInfo.RedirectStandardOutput = true;
                //startInfo.RedirectStandardError = true;

                Process process = new Process();
                process.StartInfo = startInfo;
                process.Start();
                //output = process.StandardOutput.ReadToEnd();
                //error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                GC.Collect();
            }
            catch (Exception exception)
            {
                Console.WriteLine("Standard Output " + output);
                Console.WriteLine("Standard Error " + error);
                Console.WriteLine($"Exception occured while executing {Command} {Args}");
                Console.WriteLine(exception.StackTrace);
                throw exception;
            }
        }

        public static int ParsePageNumber(string FileName)
        {
            string trimmedFileName = Path.GetFileNameWithoutExtension(FileName);
            return int.Parse(trimmedFileName.Substring(trimmedFileName.LastIndexOf("-") + 1));
        }

        public static Coordinates ParseCoordinates(string Title)
        {
            var coordinates = Title.Replace("bbox", string.Empty).Trim().Split(';').First().Split(' ')
                .Select(coord => int.Parse(coord.Trim())).ToArray();

            return new Coordinates(coordinates[0], coordinates[1], coordinates[2], coordinates[3]);
        }

        private static int WordIndexFromOffset(string Text, int Offset)
        { // change this to a functional style using zip with indexes and count with predicate
            int wordIndex = 0;
            for (int i = 0; i <= Offset; i++)
                if (Text[i] == '~') wordIndex++;
            return wordIndex;
        }

    }
}
