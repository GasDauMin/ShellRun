using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using McMaster.Extensions.CommandLineUtils;
using System.Runtime.InteropServices;
using System.Threading;

// CommandLineUtils: https://natemcmaster.github.io/CommandLineUtils/

namespace ShellRun
{
  [
    Command
    (
      UnrecognizedArgumentHandling = UnrecognizedArgumentHandling.CollectAndContinue,
      AllowArgumentSeparator = true,
      ClusterOptions = false,
      ExtendedHelpText = @"
Remarks:
  Description for allowed [-t|--type] values:
    si            Single-instance mode, create single instance with arguments.
    sir           Single-instance mode, create single instance with reorganized arguments.
    mi            Multi-instance mode, create new instance for each argument.
    mir           Multi-instance mode, create new instance for each reorganized argument.

  Description for allowed [-v|--verb] values:
    edit          Launches an editor and opens the document for editing.
    find          Initiates a search starting from the executed directory.
    open          Launches an application or its associated application.
    print         Prints the document file.
    properties    Displays the object's properties.
    runas         Launches an application as Administrator.
"
    )
  ]
  public class Program
  {
    public static void Main(string[] args) => CommandLineApplication.Execute<Program>(args);

    #region :: Generic definitions ::

    const int SW_HIDE = 0;
    const int SW_SHOW = 5;

    const string C_NULL = "";
    const int C_MAXTRY = 3;

    [DllImport("kernel32.dll")]
    static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    #endregion

    #region :: Definition of options ::

    [Option("-f|--file", "The name of a document or application file to run in the process.", CommandOptionType.SingleOrNoValue)]
    [Required]
    public string File { get; set; }

    [Option("-a|--args", "Command-line arguments to pass when starting the process.", CommandOptionType.MultipleValue)]
    public string[] Arguments { get; set; }

    [Option("-w|--workdir", "Working directory for the process to be started.", CommandOptionType.SingleOrNoValue)]
    public string Workdir { get; set; }

    [Option("-t|--type", "Type attribute defines how to run the program.", CommandOptionType.SingleValue)]
    [AllowedValues("si", "mi", "sir", "mir", IgnoreCase = true)]
    public string Type { get; set; }

    [Option("-v|--verb", "The verb attribute defines special directives on how to execute a file or launching the application.", CommandOptionType.SingleValue)]
    [AllowedValues("edit", "find", "open", "print", "properties", "runas", IgnoreCase = true)]
    public string Verb { get; set; }

    [Option("-d|--delay", "Delay specified amount of milliseconds.", CommandOptionType.SingleOrNoValue)]
    public int Delay { get; set; }

    [Option("-h|--shell", "Use shell execution.", CommandOptionType.SingleOrNoValue)]
    public bool ShellExec { get; set; }

    [Option("-p|--pass", "Encrypt run with password.", CommandOptionType.SingleOrNoValue)]
    public string Password { get; set; }

    [Option("-s|--separator", "Set default arguments separator.", CommandOptionType.SingleOrNoValue)]
    public string Separator { get; set; }

    [Option("-l|--split", "Split arguments according to specific separators.", CommandOptionType.MultipleValue)]
    public string[] Split { get; set; }

    [Option("--expand", "Expand variables.", CommandOptionType.SingleOrNoValue)]
    public bool Expand { get; set; }

    [Option("--hide", "Hide console window.", CommandOptionType.SingleOrNoValue)]
    public bool Hide { get; set; }

    [Option("--debug", "Debug mode.", CommandOptionType.SingleOrNoValue)]
    public bool Debug { get; set; }

    public string[] RemainingArguments { get; set; }

    public string[] ReorganizedArguments { get; set; }

    public string[] ProcessArguments { get; set; }

    #endregion

    #region :: Definition of general elements ::

    public void Init()
    {
      // Set default values

      Separator ??= " ";

      // Set evaluated values

      InitArguments();
    }

    private void InitArguments()
    {
      // Init <ReorganizedArguments>..

      var arguments = Arguments;
      if (arguments == null || arguments.Length <= 0)
        return;

      var list = new List<string>();

      foreach (var argument in arguments)
      {
        var value = Expand ? Environment.ExpandEnvironmentVariables(argument) : argument;

        //..

        if (Split == null)
        {
          list.Add(value);
        }
        else
        {
          list.AddRange(value.Split(Split, StringSplitOptions.None));
        }
      }

      ReorganizedArguments = list.ToArray();

      // Init <ProcessArguments>..

      arguments = IsReorganized() ? ReorganizedArguments : Arguments;
      if (arguments == null || arguments.Length <= 0)
        return;

      list = new List<string>();

      if (Type == "si" || Type == "sir")
      {
        list.Add(string.Join(Separator, arguments));
      }
      else
      {
        list.AddRange(arguments);
      }

      ProcessArguments = list.ToArray();
    }

    public bool IsMultiInstance()
    {
      return (Type == "mir" || Type == "mi");
    }

    public bool IsSingleInstance()
    {
      return (Type == "sir" || Type == "si");
    }

    public bool IsReorganized()
    {
      return (Type == "mir" || Type == "sir");
    }

    public bool IsLocked()
    {
      return (Password != null);
    }

    public bool TryUnlock()
    {
      var success = false;
      var attempt = C_MAXTRY;

      if (!this.IsLocked())
        return true;

      do
      {
        if (Password == Prompt.GetPassword("Enter password: "))
        {
          success = true;
        }
        else
        {
          attempt--;

          Console.ForegroundColor = ConsoleColor.Red;
          Console.Error.WriteLine(attempt > 0 ? $"Wrong password, attempts left {attempt}." : "Authorization failed! :-(");
          Console.ResetColor();
        }
      } while (!success && attempt > 0);

      return success;
    }

    public void TryRun()
    {
      void run(string arguments = null)
      {
        var processInfo = new ProcessStartInfo {FileName = File};

        if (arguments != null)
          processInfo.Arguments = arguments;

        if (Workdir != null)
          processInfo.WorkingDirectory = Workdir;

        if (Verb != null)
          processInfo.Verb = Verb;

        if (ShellExec)
          processInfo.UseShellExecute = true;

        if (Hide)
        {
          processInfo.CreateNoWindow = true;
          processInfo.RedirectStandardError = false;
          processInfo.RedirectStandardOutput = false;
          processInfo.WindowStyle = ProcessWindowStyle.Hidden;
        }

        Process.Start(processInfo);
      }
      
      //..

      if (ProcessArguments == null)
      {
        run();
      }
      else
      {
        for (var i = 0; i < ProcessArguments.Length; i++)
        {
          run(ProcessArguments[i]);

          if (Delay > 0 && (i != ProcessArguments.Length - 1))
          { 
            Thread.Sleep(Delay);
          }
        }
      }
    }

    #endregion

    #region :: Definition of debuging elements ::

    private void PrintDebugArray(string name, string[] array)
    {
      var format = "  {0,-10} => {1}";

      if (array == null)
        return;

      for (var i = 0; i < array.Length; i++)
      {
        Console.WriteLine(format, $"{name}[{i}]", array[i]);
      }
    }

    private void PrintDebugValue(string name, object element)
    {
      var format = "  {0,-10} => {1}";
      var value = C_NULL;

      if (element != null)
      {
        var type = element.GetType();

        if (type.IsArray)
        {
          var arr = Array.ConvertAll((object[])element, Convert.ToString);
          value = "[" + (string.Join("], [", arr)) + "]";
        }
        else
        {
          value = $"[{element}]";
        }
      }

      Console.WriteLine(format, name, value);
    }

    public void PrintDebugInfo()
    {
      Console.WriteLine("Variables:");
      PrintDebugValue("File", File);
      PrintDebugValue("Workdir", Workdir);
      PrintDebugValue("Type", Type);
      PrintDebugValue("Verb", Verb);
      PrintDebugValue("Split", Split);
      PrintDebugValue("Separator", Separator);
      PrintDebugValue("Delay", Delay);
      PrintDebugValue("Expand", Expand);
      PrintDebugValue("Shell", ShellExec);
      PrintDebugValue("Hide", Hide);
      PrintDebugValue("Lock", IsLocked());

      Console.WriteLine("\r\nProcess arguments:");
      PrintDebugArray("Item", ProcessArguments);

      Console.WriteLine("\r\nInitial arguments:");
      PrintDebugArray("Item", Arguments);

      Console.WriteLine("\r\nUnrecognized arguments:");
      PrintDebugArray("Item", RemainingArguments);
    }

    #endregion  

    private void OnExecute()
    {
      this.Init();

      if (!TryUnlock())
      {
        Console.ReadLine();
        return;
      }

      Console.Clear();

      if (Hide)
      {
        IntPtr h = Process.GetCurrentProcess().MainWindowHandle;
        ShowWindow(h, SW_HIDE);
      }

      if (Debug)
      {
        PrintDebugInfo();
        Console.ReadLine();
        return;
      }

      TryRun();
    }
  }
}
