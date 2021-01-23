using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using McMaster.Extensions.CommandLineUtils;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;
using NLog.Fluent;
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

      if (args.Contains("-x"))
      {
        var fileName = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        var arguments = args.Where(arg => arg != "-x");

        if (fileName != null)
        {
          var processInfo = new ProcessStartInfo
          {
            FileName = fileName, 
            Arguments = string.Join(" ", arguments), 
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
            Logger.Error("Unable to assign administrator permission.");
          }
        }
      }

      CommandLineApplication.Execute<Program>(args);

      Logger.Debug("Application finished");
    }

    private void OnExecute()
    {
      Logger.Debug("Execution started");

      try
      {
        this.Init();
        this.Check();
        this.Debug();
        
        switch (Action)
        {
          case ACTION.RUN: Run(); break;
          case ACTION.DEBUG: this.Debug(LogLevel.Info); break;
        }

        Pause();
      }
      catch (Exception ex)
      {
        Logger.Fatal($"Exception:\r\n{ex}");
      }

      Logger.Debug("Execution finished");
    }

    #region :: Generic definitions ::

    const int SW_HIDE = 0;
    const int SW_SHOW = 5;

    const string C_NULL = "";
    const int C_MAXTRY = 3;

    public enum ACTION
    {
      RUN,
      DEBUG,
      EXIT
    }

    [DllImport("kernel32.dll")]
    static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

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

    public ACTION Action { get; set; }

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

      Action = ACTION.RUN;

      if (FlagDebug())
      {
        Action = ACTION.DEBUG;
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

      if (FlagHide() && Action == ACTION.RUN)
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
        if (Action != ACTION.DEBUG)
        {
          ok = false;
          Logger.Error($"File '{File}' does not exists.");
        }
      }

      //..

      if (!ok)
      {
        Action = ACTION.EXIT;
      }
    }

    public bool Unlock()
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

    public void Run()
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

    public void Pause()
    {
      if (FlagPause())
      {
        Console.ReadLine();
      }
    }

    #endregion

    #region :: Definition of logging elements ::

    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    private void Debug(LogLevel level = null)
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
      Logger.Log(level, " [Action]     => {@value}", Action);
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

    #endregion

  }
}
