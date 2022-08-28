using Ae.RemoteManager.Console;
using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Ae.Dns.Console
{
    class Program
    {
        static void Main(string[] args) => DoWork(args).GetAwaiter().GetResult();

        private static async Task DoWork(string[] args)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var configuration = JsonSerializer.Deserialize<RemoteManagerConfiguration>(File.OpenRead("config.json"), options);

            var services = new ServiceCollection();
            services.AddLogging(x => x.AddConsole());
            var provider = services.BuildServiceProvider();

            var logger = provider.GetRequiredService<ILogger<Program>>();

            var tasks = configuration.Instructions.Where(x => x.Value.Enabled).Select(x => RunInstructionForever(x, logger, CancellationToken.None));
            await Task.WhenAll(tasks);
        }

        private static async Task RunInstructionForever(KeyValuePair<string, RemoteInstructions> instructions, ILogger logger, CancellationToken token)
        {
            CronExpression expression = CronExpression.Parse(instructions.Value.Cron);

            do
            {
                DateTime nextUtc = expression.GetNextOccurrence(DateTime.UtcNow) ?? throw new InvalidOperationException($"Unable to get next occurance of {expression}");
                var delay = nextUtc - DateTime.UtcNow;

                logger.LogInformation("Waiting for {Delay} to start {Instruction} (cron: {Cron})", delay, instructions.Key, expression);

                if (!instructions.Value.Testing)
                {
                    await Task.Delay(delay, token);
                }

                logger.LogInformation("Running instruction {Instruction}", instructions.Key);

                var sw = Stopwatch.StartNew();
                try
                {
                    RunInstructions(instructions.Value, logger);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Encountered exception whilst running instruction");
                }
                finally
                {
                    logger.LogInformation("Instruction completed in {ElapsedSeconds}s", sw.Elapsed.TotalSeconds);
                }
            }
            while (!token.IsCancellationRequested && !instructions.Value.Testing);
        }

        private static void RunInstructions(RemoteInstructions instructions, ILogger logger)
        {
            if (instructions.Password == null)
            {
                using var privateKeyStream = File.OpenRead(instructions.PrivateKeyFile);

                using var privateKeyFile = new PrivateKeyFile(privateKeyStream);
                RsaSha256Util.ConvertToKeyWithSha256Signature(privateKeyFile);

                using var privateKeyAuthenticationMethod = new PrivateKeyAuthenticationMethod(instructions.Username, privateKeyFile);

                var connectionInfo = new ConnectionInfo(instructions.Endpoint, instructions.Username, privateKeyAuthenticationMethod);
                RsaSha256Util.SetupConnection(connectionInfo); // adds rsa-sha2-256

                using var sshClient = new SshClient(connectionInfo);
                RunCommands(sshClient, logger, instructions);
            }
            else
            {
                using var sshClient = new SshClient(instructions.Endpoint, instructions.Username, instructions.Password);
                RunCommands(sshClient, logger, instructions);
            }
        }

        private static void RunCommands(SshClient client, ILogger logger, RemoteInstructions instructions)
        {
            client.Connect();

            foreach (var command in instructions.Commands)
            {
                RunCommand(client, logger, command);
            }

            client.Disconnect();
        }

        private static void RunCommand(SshClient client, ILogger logger, string command)
        {
            var sw = Stopwatch.StartNew();
            using var cmd = client.CreateCommand(command);
            logger.LogInformation("Executing {CommandText}", cmd.CommandText);
            cmd.Execute();

            if (cmd.ExitStatus != 0)
            {
                throw new InvalidOperationException($"Command {cmd.CommandText} failed in {sw.Elapsed.TotalSeconds} with error {cmd.Error.Trim()}");
            }

            logger.LogInformation("Executed {CommandText} in {ElapsedSeconds}s. Result: {Result}", command, sw.Elapsed.TotalSeconds, cmd.Result.Trim());
        }
    }
}
