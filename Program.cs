using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using McMaster.Extensions.CommandLineUtils;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;
using System.Text.RegularExpressions;
using NLog;

using LogLevel = NLog.LogLevel;

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

  Description for allowed [-f|--flag] values:
    d             Debug mode.
    e             Expand environment variables.
    h             Hide console window.
    p             Pause console and wait for input.
    s             Use shell execution.

  Description for allowed [-u|--unicode] values:
    i             Standard input encoding UTF-8.
    o             Standard output encoding UTF-8.
    e             Standard error encoding UTF-8.
"
    )
  ]
  public class Program
  {
    public static void Main(string[] args)
    {
      Logger.Debug("Application started");

      Console.OutputEncoding = Encoding.UTF8;
      Console.InputEncoding = Encoding.UTF8;

      var command = Marshal.PtrToStringAuto(GetCommandLine()); //Alternative for [System.Environment.CommandLine;]
      if (command != null && args.Contains("-x"))
      {
        var filename = command.Split(' ')[0];
        var arguments = command.Substring(filename.Length, (command.Length - filename.Length));

        if (!string.IsNullOrEmpty(filename))
        {
          arguments = Regex.Replace(arguments, "(^| )-x($| )", "$1$2");

          var processInfo = new ProcessStartInfo
          {
            FileName = filename, 
            Arguments = arguments, 
            Verb = "runas", 
            UseShellExecute = true
          };

          try
          {
            System.Diagnostics.Process.Start(processInfo);
            System.Environment.Exit(1);
          }
          catch (System.ComponentModel.Win32Exception ex)
          {
            Logger.Fatal($"Exception:\r\n{ex}");
          }
        }
      }

      CommandLineApplication.Execute<Program>(args);

      Logger.Debug("Application finished");
    }

    private void OnExecute()
    {
      try
      {
        Logger.Debug("Init [1/5]");

        this.Init();

        Logger.Debug("Check [2/5]");

        this.Check();

        Logger.Debug("Debug [3/5]");

        this.Debug();

        Logger.Debug("Action [4/5]");

        this.Action();

        Logger.Debug("End [5/5]");

      }
      catch (Exception ex)
      {
        Logger.Fatal($"Exception:\r\n{ex}");
      }
    }

    #region :: Generic definitions ::

    const int SW_HIDE = 0;
    const int SW_SHOW = 5;

    const string C_NULL = "";
    const int C_MAXTRY = 3;

    public enum ACTION
    {
      EXECUTE,
      DEBUG,
      EXIT
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern System.IntPtr GetCommandLine();

    [DllImport("kernel32.dll")]
    static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    #endregion

    #region :: Definition of options ::

    [Option("-f|--file", "The name of a document or application file to run in the process.", CommandOptionType.SingleOrNoValue)]
    public string File { get; set; }

    [Option("-a|--args", "Command-line arguments to pass when starting the process.", CommandOptionType.MultipleValue)]
    public string[] Arguments { get; set; }

    [Option("-t|--type", "Type attribute defines how to run the program.", CommandOptionType.SingleValue)]
    [AllowedValues("si", "mi", "sir", "mir", IgnoreCase = true)]
    public string Type { get; set; }

    [Option("-g|--flag", "Runtime flags.", CommandOptionType.SingleOrNoValue)]
    public string Flag { get; set; }

    [Option("-u|--unicode", "Unicode flags.", CommandOptionType.SingleOrNoValue)]
    public string Unicode { get; set; }

    [Option("-v|--verb", "The verb attribute defines special directives on how to execute a file or launching the application.", CommandOptionType.SingleValue)]
    [AllowedValues("edit", "find", "open", "print", "properties", "runas", IgnoreCase = true)]
    public string Verb { get; set; }

    [Option("-d|--delay", "Delay specified amount of milliseconds.", CommandOptionType.SingleOrNoValue)]
    public int Delay { get; set; }

    [Option("-p|--pass", "Encrypt run with password.", CommandOptionType.SingleOrNoValue)]
    public string Password { get; set; }

    [Option("-s|--spt", "Set default arguments separator.", CommandOptionType.SingleOrNoValue)]
    public string Separator { get; set; }

    [Option("-l|--split", "Split arguments according to specific separators.", CommandOptionType.MultipleValue)]
    public string[] Split { get; set; }

    [Option("-w|--workdir", "Working directory for the process to be started.", CommandOptionType.SingleOrNoValue)]
    public string Workdir { get; set; }

    [Option("-q|--quotation", "Quotation marks for reorganized arguments.", CommandOptionType.SingleOrNoValue)]
    public string Quotation { get; set; }

    public string[] RemainingArguments { get; set; }

    public string[] ReorganizedArguments { get; set; }

    public string[] ProcessArguments { get; set; }

    public string[] ExecutionArguments { get; set; }

    public ACTION ActionValue { get; set; }

    #endregion

    #region :: Definition of flags ::

    public bool FlagDebug() => Flag.ToLower().Contains("d");

    public bool FlagExpand() => Flag.ToLower().Contains("e");

    public bool FlagShell() => Flag.ToLower().Contains("s");

    public bool FlagPause() => Flag.ToLower().Contains("p");

    public bool FlagHide() => Flag.ToLower().Contains("h");

    public bool FlagLocked() => (Password != null);

    public bool FlagUnicodeInput() => Unicode.ToLower().Contains("i");

    public bool FlagUnicodeOutput() => Unicode.ToLower().Contains("o");

    public bool FlagUnicodeErrors() => Unicode.ToLower().Contains("e");

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

    #endregion

    #region :: Definition of general elements ::

    public void Init()
    {
      // Set default values

      Separator ??= " ";
      Unicode ??= "";
      Flag ??= "";

      // Set action value

      ActionValue = ACTION.EXECUTE;

      if (FlagDebug())
      {
        ActionValue = ACTION.DEBUG;
      }
      
      // Init <ReorganizedArguments>..

      var list = new List<string>();
      var arguments = Arguments;
      if (arguments != null && arguments.Length > 0)
      {
        foreach (var argument in arguments)
        {
          var value = FlagExpand() ? Environment.ExpandEnvironmentVariables(argument) : argument;

          if (Split == null)
          {
            list.Add(value);
          }
          else
          {
            list.AddRange(value.Split(Split, StringSplitOptions.None));
          }

          if (Quotation != null)
          {
            var format = (Quotation + "{0}" + Quotation);
            list = list.Select(x => string.Format(format, x)).ToList();
          }
        }
      }

      ReorganizedArguments = list.ToArray();

      // Init <ProcessArguments>..

      list = new List<string>();
      arguments = IsReorganized() ? ReorganizedArguments : Arguments;
      if (arguments != null && arguments.Length > 0)
      {
        if (Type == "si" || Type == "sir")
        {
          list.Add(string.Join(Separator, arguments));
        }
        else
        {
          list.AddRange(arguments);
        }
      }

      ProcessArguments = list.ToArray();

      // Init <ExecutionArguments>..

      ExecutionArguments = ProcessArguments.Select(x => $"\"{File}\" {x}").ToArray();

      // Other operations..

      if (FlagHide() && ActionValue == ACTION.EXECUTE)
      {
        IntPtr h = Process.GetCurrentProcess().MainWindowHandle;
        ShowWindow(h, SW_HIDE);
      }
    }

    public void Check()
    {
      var ok = true;

      // Try unlock..

      if (FlagLocked())
      {
        if (Unlock() == false)
        {
          ok = false;
        }
      }

      // Test values..

      if (!System.IO.File.Exists(File))
      {
        if (ActionValue != ACTION.DEBUG)
        {
          ok = false;
          Logger.Error($"File '{File}' does not exists.");
        }
      }

      //..

      if (!ok)
      {
        ActionValue = ACTION.EXIT;
      }
    }

    public void Debug(LogLevel level = null)
    {
      level ??= LogLevel.Debug;

      var padWidth = 70;
      var padChar = '-';

      Logger.Log(level, "".PadLeft(padWidth, padChar));
      Logger.Log(level, " VALUES");
      Logger.Log(level, "".PadLeft(padWidth, padChar));

      Logger.Log(level, " [File]       => {@value}", File);
      Logger.Log(level, " [Workdir]    => {@value}", Workdir);
      Logger.Log(level, " [Type]       => {@value}", Type);
      Logger.Log(level, " [Verb]       => {@value}", Verb);
      Logger.Log(level, " [Flag]       => {@value}", Flag);
      Logger.Log(level, " [Split]      => {@value}", Split);
      Logger.Log(level, " [Delay]      => {@value}", Delay);
      Logger.Log(level, " [Action]     => {@value}", ActionValue);
      Logger.Log(level, " [Unicode]    => {@value}", Unicode);
      Logger.Log(level, " [Separator]  => {@value}", Separator);
      Logger.Log(level, " [Quotation]  => {@value}", Quotation);

      Logger.Log(level, "".PadLeft(padWidth, padChar));
      Logger.Log(level, " FLAGS");
      Logger.Log(level, "".PadLeft(padWidth, padChar));

      Logger.Log(level, " [FlagDebug]  => {@value}", FlagDebug());
      Logger.Log(level, " [FlagExpand] => {@value}", FlagExpand());
      Logger.Log(level, " [FlagShell]  => {@value}", FlagShell());
      Logger.Log(level, " [FlagHide]   => {@value}", FlagHide());
      Logger.Log(level, " [FlagLocked] => {@value}", FlagLocked());

      Logger.Log(level, "".PadLeft(padWidth, padChar));
      Logger.Log(level, " ARGUMENTS");
      Logger.Log(level, "".PadLeft(padWidth, padChar));

      Logger.Log(level, " [Execution]  => {@value}", ExecutionArguments);
      Logger.Log(level, " [Process]    => {@value}", ProcessArguments);
      Logger.Log(level, " [Initial]    => {@value}", Arguments);
      Logger.Log(level, " [Unknown]    => {@value}", RemainingArguments);

      Logger.Log(level, "".PadLeft(padWidth, padChar));
    }

    public void Action()
    {
      switch (ActionValue)
      {
        case ACTION.EXECUTE: this.Execute(); break;
        case ACTION.DEBUG: this.Debug(LogLevel.Info); break;
      }

      Pause();
    }

    private bool Unlock()
    {
      var success = false;
      var attempt = C_MAXTRY;

      if (!this.FlagLocked())
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

          Logger.Warn(attempt > 0 ? $"Wrong password, attempts left {attempt}." : "Authorization failed! :-(");
        }
      } while (!success && attempt > 0);

      return success;
    }

    private void Execute()
    {
      void run(string arguments = null)
      {
        var processInfo = new ProcessStartInfo(File);

        if (arguments != null)
          processInfo.Arguments = arguments;

        if (Workdir != null)
          processInfo.WorkingDirectory = Workdir;

        if (Verb != null)
          processInfo.Verb = Verb;

        if (FlagShell())
          processInfo.UseShellExecute = true;

        if (FlagHide())
        {
          processInfo.CreateNoWindow = true;
          processInfo.RedirectStandardError = false;
          processInfo.RedirectStandardOutput = false;
          processInfo.WindowStyle = ProcessWindowStyle.Hidden;
        }

        if (FlagUnicodeOutput())
        {
          processInfo.StandardOutputEncoding = Encoding.UTF8;
          processInfo.RedirectStandardOutput = true;
        }

        if (FlagUnicodeErrors())
        {
          processInfo.StandardErrorEncoding = Encoding.UTF8;
          processInfo.RedirectStandardError = true;
        }

        Process.Start(processInfo);
      }

      //..
      
      Console.Clear();

      if (ProcessArguments == null || ProcessArguments.Length == 0)
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

    private void Pause()
    {
      if (FlagPause())
      {
        Console.ReadLine();
      }
    }

    #endregion

  }
}
