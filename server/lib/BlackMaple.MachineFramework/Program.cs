﻿/* Copyright (c) 2018, John Lenz

All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

    * Redistributions of source code must retain the above copyright
      notice, this list of conditions and the following disclaimer.

    * Redistributions in binary form must reproduce the above
      copyright notice, this list of conditions and the following
      disclaimer in the documentation and/or other materials provided
      with the distribution.

    * Neither the name of John Lenz, Black Maple Software, SeedTactics,
      nor the names of other contributors may be used to endorse or
      promote products derived from this software without specific
      prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("BlackMaple.MachineFramework.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("BlackMaple.MachineFramework.DebugMock")]

namespace BlackMaple.MachineFramework
{
  public class Program
  {
    private static (IConfiguration, ServerSettings) LoadConfig()
    {
      var configFile = Path.Combine(ServerSettings.ConfigDirectory, "config.ini");
      if (!File.Exists(configFile))
      {
        var defaultConfigFile = Path.Combine(ServerSettings.ContentRootDirectory, "default-config.ini");
        if (File.Exists(defaultConfigFile))
        {
          if (!Directory.Exists(ServerSettings.ConfigDirectory))
            Directory.CreateDirectory(ServerSettings.ConfigDirectory);
          System.IO.File.Copy(defaultConfigFile, configFile, overwrite: false);
        }
      }

      var cfg =
          new ConfigurationBuilder()
          .AddIniFile(configFile, optional: true)
          .AddEnvironmentVariables()
          .Build();

      var s = ServerSettings.Load(cfg);

      return (cfg, s);
    }

    private static void EnableSerilog(ServerSettings serverSt, bool enableEventLog)
    {
      var logConfig = new LoggerConfiguration()
          .MinimumLevel.Debug()
          .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
          .WriteTo.Console(restrictedToMinimumLevel:
              serverSt.EnableDebugLog ?
                  Serilog.Events.LogEventLevel.Debug
                : Serilog.Events.LogEventLevel.Information);

#if SERVICE_AVAIL
            if (enableEventLog) {
                logConfig = logConfig.WriteTo.EventLog(
                    "FMS Insight",
                    manageEventSource: true,
                    restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information);
            }
#endif

      if (serverSt.EnableDebugLog)
      {
        logConfig = logConfig.WriteTo.File(
            new Serilog.Formatting.Compact.CompactJsonFormatter(),
            System.IO.Path.Combine(ServerSettings.ConfigDirectory, "fmsinsight-debug.txt"),
            rollingInterval: RollingInterval.Day,
            restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Debug);
      }

      Log.Logger = logConfig.CreateLogger();
    }

    public static IWebHost BuildWebHost(IConfiguration cfg, ServerSettings serverSt, FMSSettings fmsSt, FMSImplementation fmsImpl)
    {
      return new WebHostBuilder()
          .UseConfiguration(cfg)
          .ConfigureServices(s =>
          {
            s.AddSingleton<FMSImplementation>(fmsImpl);
            s.AddSingleton<FMSSettings>(fmsSt);
            s.AddSingleton<ServerSettings>(serverSt);
          })
          .SuppressStatusMessages(suppressStatusMessages: true)
          .UseKestrel(options =>
          {
            var address = IPAddress.IPv6Any;
            if (!string.IsNullOrEmpty(serverSt.TLSCertFile))
            {
              options.Listen(address, serverSt.Port, listenOptions =>
              {
                listenOptions.UseHttps(serverSt.TLSCertFile);
              });
            }
            else
            {
              options.Listen(address, serverSt.Port);
            }
          })
          .UseContentRoot(ServerSettings.ContentRootDirectory)
          .UseSerilog()
          .UseStartup<Startup>()
          .Build();
    }

    public static void Run(bool useService, Func<IConfiguration, FMSSettings, FMSImplementation> initalize, bool outputConfigToLog = true)
    {
      var (cfg, serverSt) = LoadConfig();
      EnableSerilog(serverSt: serverSt, enableEventLog: useService);

      FMSImplementation fmsImpl;
      FMSSettings fmsSt;
      try
      {
        fmsSt = new FMSSettings(cfg);
        if (outputConfigToLog)
        {
          Log.Information("Starting FMS Insight with settings {@ServerSettings} and {@FMSSettings}. " +
                          " Using ContentRoot {ContentRoot} and Config {ConfigDir}.",
              serverSt, fmsSt, ServerSettings.ContentRootDirectory, ServerSettings.ConfigDirectory);
        }
        fmsImpl = initalize(cfg, fmsSt);
      }
      catch (Exception ex)
      {
        Serilog.Log.Error(ex, "Error initializing FMS Insight");
        return;
      }
      var host = BuildWebHost(cfg, serverSt, fmsSt, fmsImpl);

#if SERVICE_AVAIL
      if (useService)
      {
        Microsoft.AspNetCore.Hosting.WindowsServices.WebHostWindowsServiceExtensions
          .RunAsService(host);
      } else {
        host.Run();
      }
#else
      host.Run();
#endif
    }
  }
}
