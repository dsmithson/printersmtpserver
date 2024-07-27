using System;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using SmtpServerServiceLibrary.Legacy;

CancellationTokenSource _cts = new CancellationTokenSource();

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter())
    .CreateLogger();

// Register shutdown handlers
Console.CancelKeyPress += (sender, e) =>
{
    Log.Information("Process exit requested.");
    e.Cancel = true; // Prevent the process from terminating immediately
    _cts.Cancel();
};

// Handle termination signals
AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) =>
{
    Log.Information("Process exit requested.");
    _cts.Cancel();
};

try
{
    var settings = HomePrinterRelaySettings.LoadSettings(args);
    if (settings == null)
    {
        Log.Error("Failed to load settings.");
        return;
    }
    if(string.IsNullOrEmpty(settings.FilePath))
    {
        Log.Error("Output path not specified.");
        return;
    }

    Log.Information("Starting up...");
    Log.Information("Listening for SMTP requests on port {0}", settings.SmtpPort);
    Log.Information("Email attachments will be saved to {0}", settings.FilePath);
    var relayServer = new HomePrinterRelay(settings);
    relayServer.Startup();

    var token = _cts.Token;
    token.Register(() =>
    {
        Log.Information("Shutdown initiated.");
        relayServer.Shutdown();
        Log.Information("Shutdown complete.");
    });

    // Let main app wait indefinately (or until token is cancelled)
    await Task.Delay(Timeout.Infinite, token);
}
catch (OperationCanceledException)
{
    // Handle cancellation if needed
    Log.Information("Application exit requested.");
}
finally
{
    // Ensure to flush logs and perform any final cleanup
    Log.Information("Application exiting...");
    Log.CloseAndFlush();
}
