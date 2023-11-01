# ShellRun

ShellRun is a command line utility that allows you to run shell commands with arguments and options.

## Arguments

Use the ShellRun command with the following syntax:

`ShellRun [options] [[--] <arg>...]`

### Options:

- `-?|-h|--help:` Show help information.
- `-f|--file:` The name of a document or application file to run in the process.
- `-a|--args:` Command-line arguments to pass when starting the process.
- `-t|--type:` Type attribute defines how to run the program.
- `-g|--flag:` Runtime flags.
- `-u|--unicode:` Unicode flags.
- `-v|--verb:` The verb attribute defines special directives on how to execute a file or launching the application.
- `-d|--delay:` Delay specified amount of milliseconds. Default value is: 0.
- `-p|--pass:` Encrypt run with password.
- `-s|--spt:` Set default arguments separator.
- `-l|--split:` Split arguments according to specific separators.
- `-w|--workdir:` Working directory for the process to be started.
- `-q|--quotation:` Quotation marks for reorganized arguments.

### Remarks:

Description for allowed `[-t|--type]:` values:
- `si` Single-instance mode, create single instance with arguments.
- `sir` Single-instance mode, create single instance with reorganized arguments.
- `mi` Multi-instance mode, create new instance for each argument.
- `mir` Multi-instance mode, create new instance for each reorganized argument.

Description for allowed `[-v|--verb]:` values:
- `edit` Launches an editor and opens the document for editing.
- `find` Initiates a search starting from the executed directory.
- `open` Launches an application or its associated application.
- `print` Prints the document file.
- `properties` Displays the object's properties.
- `runas` Launches an application as Administrator.

Description for allowed `[-g|--flag]:` values:
- `d` Debug mode.
- `e` Expand environment variables.
- `h` Hide console window.
- `p` Pause console and wait for input.
- `s` Use shell execution.

Description for allowed `[-u|--unicode]:` values:
- `i` Standard input encoding UTF-8.
- `o` Standard output encoding UTF-8.
- `e` Standard error encoding UTF-8.

## Example

```
ShellRun.exe -f:".\foo\bar.exe" -a:"file.txt" -l:";" -q:"\"" -t:mir
```