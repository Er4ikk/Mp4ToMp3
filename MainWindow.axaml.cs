using System.Diagnostics;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace Mp4ToMp3;

public partial class MainWindow : Window
{
    string outputDirectory = "out";
    string outputFileType = ".mp3";
    string inputFolder = "";
    string inputFile = "";
    string option = "";
    private ComboBox ComboBoxOutputFormat;


    public MainWindow()
    {
        InitializeComponent();
                ComboBoxOutputFormat = this.FindControl<ComboBox>("ComboBox");
        if(ComboBoxOutputFormat != null)
        {
            ComboBoxOutputFormat.SelectionChanged += ComboBoxOutputFormat_SelectionChanged;
        }
    }

        private void ComboBoxOutputFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {

        string selectedValue = (ComboBoxOutputFormat.SelectedItem as ComboBoxItem)?.Content?.ToString();

        if (!string.IsNullOrEmpty(selectedValue))
        {
            outputFileType = selectedValue;
        }
    }

    private async void Button_OnClick(object? sender, RoutedEventArgs e)
    {

        if (inputFolder != "")
            await Convert([PathOptions.FOLDER, inputFolder, FormatOption.FORMAT, outputFileType]);
        else
            ConversionStatus.Text = "Please select a folder first.";
    }

    private async void GetFolderPath(object? sender, RoutedEventArgs e)
    {
        // Get top level from the current control. Alternatively, you can use Window reference instead.
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {

            // Start async operation to open the dialog.
            var folder = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select a folder",
                AllowMultiple = false
            });

            if (folder.Count >= 1)
            {
                // Open reading stream from the first file.

                string path = folder[0].Path.LocalPath;
                Output.Text = "Output Path: " + path;
                Debug.WriteLine("Selected folder path: " + path);
                inputFolder = path;
            }
        }
        else
        {
            Debug.WriteLine("TopLevel is null");
        }
    }


    public async Task Convert(string[] args)
    {
        //clean out directory
        if (Directory.Exists(outputDirectory))
        {
            DeleteFilesInsideDirectory(outputDirectory);
        }
        else
        {
            Directory.CreateDirectory(outputDirectory);
        }

        if (args != null)
        {
            ManageArguments(args);
        }
        else
        {
            PrintUsages();
        }

        await StartConversion();
    }

    public void PrintUsages()
    {

    }

    public void ManageArguments(string[] args)
    {
        if (args[0] != null)
        {
            option = args[0];
        }
        else
        {
            option = "-single_file";
        }

        if (args[1] != null)
        {
            ManagePathMode(option, args[1]);

        }
        else
        {
            throw new Exception("No specified path");
        }

        if (args.Length > 2)
        {
            if (args[2] != null && args[3] != null)
            {
                ManageFormatOption(args[2], args[3]);
            }
            else
            {
                throw new Exception("Invalid format operation");
            }
        }


    }

    public void ManagePathMode(string option, string path)
    {

        if (option != "-single_file")
        {
            inputFolder = path;
        }
        else
        {
            inputFile = path;
        }

    }

    public void ManageFormatOption(string formatOption, string formatSpecified)
    {
        if (formatOption == FormatOption.FORMAT)
        {
            Debug.WriteLine(formatSpecified);
            if (AvailableFormat.AVAILABLE_FORMATS.Contains(formatSpecified))
            {
                outputFileType = formatSpecified;
            }
            else
            {
                throw new Exception("Format not supported: " + formatSpecified);
            }
        }
    }

    public async Task ConvertMultipleFilesInFolder(string? folderPath)
    {
        string[] filePaths;
        if (folderPath != null)
        {
            ConversionStatus.Text = "Getting files from folder: " + folderPath;
            filePaths = Directory.GetFiles(folderPath);

            ConversionStatus.Text = "\n Starting converting files...\n";


            foreach (string filePath in filePaths)
            {
                ConversionStatus.Text += "\n Processing file: " + filePath + "\n";

                await ConvertSingleFileAsync(filePath);
            }

            // await Task.WhenAll(tasks);
            ConversionStatus.Text += "\n Finished converting files\n";
        }
    }

    public async Task StartConversion()
    {
        if (option == PathOptions.SINGLE_FILE)
        {
            if (inputFile != null)
            {
                await ConvertSingleFileAsync(inputFile);
            }
            else
            {
                throw new Exception("No specified file path");
            }
        }
        else if (option == PathOptions.FOLDER)
        {
            if (inputFolder != null)
            {
                await ConvertMultipleFilesInFolder(inputFolder);
            }
            else
            {
                throw new Exception("No specified folder path");
            }
        }
        else
        {
            throw new Exception("Uknown option: " + option);
        }
    }

    public async Task ConvertSingleFileAsync(string? inputFilePath)
    {
        if (inputFilePath != null)
        {
            // Debug.WriteLine("is windows: "+isWindows + " directory path"+directoryDelimiter);
            string fileName = inputFilePath.Split(Path.DirectorySeparatorChar).Last();
            ConversionStatus.Text += "\n Start processing file: " + fileName + "\n";
            // Debug.WriteLine("Start processing file:" + fileName);
            if (!File.Exists("ffmpeg") || !File.Exists("ffprobe"))
            {
                ConversionStatus.Text += "\n Downloading ffmpeg executables...\n";
                // Debug.WriteLine("Started downloading ffmpeg executables");
                await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official);
                ConversionStatus.Text += "\n Finished downloading ffmpeg executables\n";
                // Debug.WriteLine("Finish download ffmpeg");
            }


            Debug.WriteLine(Directory.GetCurrentDirectory());
            FFmpeg.SetExecutablesPath(Directory.GetCurrentDirectory(), "ffmpeg", "ffprobe");
            string output = Path.ChangeExtension(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + outputDirectory + Path.DirectorySeparatorChar + fileName, outputFileType);
            var snippet = await FFmpeg.Conversions.FromSnippet.ExtractAudio(inputFilePath, output);
            ConversionStatus.Text += "\n Converting to " + outputFileType + " format...\n";

            //load percentage
            snippet.OnProgress += (sender, args) =>
            {
                var percent = (int)(Math.Round(args.Duration.TotalSeconds / args.TotalLength.TotalSeconds, 2) * 100);
                // ConversionStatus.Text += $"\r[{args.Duration} / {args.TotalLength}] {percent}%";
                // Debug.WriteLine($"[{args.Duration} / {args.TotalLength}] {percent}%");
            };

            IConversionResult result = await snippet.Start();
            ConversionStatus.Text += "\n Finished converting file: " + fileName + "\n";

            // Debug.WriteLine("Finished conversion results are at: " + output);
        }
    }

    public void DeleteFilesInsideDirectory(string directoryPath)
    {
        System.IO.DirectoryInfo di = new DirectoryInfo(directoryPath);

        if (di.GetFiles().Length > 0)
        {
            Debug.WriteLine("Cleaning out directory");
            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
            }
        }

    }



}