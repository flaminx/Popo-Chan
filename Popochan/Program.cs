using System;
using System.Configuration;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Tweetinvi;
using Tweetinvi.Models;
using Tweetinvi.Streaming;

namespace Popochan
{
    internal static class manageScanner
    {
        public static bool isRunning { get; set; }
        public static IFilteredStream stream { get; set; }
    }

    internal class Program
    {
        private DiscordSocketClient _client;

        //private List<String> stList;
        private CommandService _commands;
        private IServiceProvider _services;

        private static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            manageScanner.isRunning = false;
            if (ConfigurationManager.AppSettings["AccessToken"] == "")
                try
                {
                    // Create a new set of credentials for the application.
                    var appCredentials = new TwitterCredentials("yourkey",
                        "yoursecret");

                    // Init the authentication process and store the related `AuthenticationContext`.
                    var authenticationContext = AuthFlow.InitAuthentication(appCredentials);

                    // Go to the URL so that Twitter authenticates the user and gives him a PIN code.
                    Process.Start(authenticationContext.AuthorizationURL);

                    // Ask the user to enter the pin code given by Twitter
                    Console.WriteLine("Enter the PIN given to you by Twitter:");
                    var pinCode = Console.ReadLine();

                    // With this pin code it is now possible to get the credentials back from Twitter
                    var userCredentials = AuthFlow.CreateCredentialsFromVerifierCode(pinCode, authenticationContext);
                    // Use the user credentials in your application

                    ConfigurationManager.AppSettings["AccessToken"] = userCredentials.AccessToken;
                    ConfigurationManager.AppSettings["AccessTokenSecret"] = userCredentials.AccessTokenSecret;

                    Auth.SetCredentials(userCredentials);
                }
                //Yeah catching all exceptions is lazy, bite me.
                catch (Exception)
                {
                    {
                        Console.WriteLine("Uh oh! Something went Wrong, Press a key to shutdown!");
                        Console.ReadKey();
                        Environment.Exit(0);
                    }
                }
            else
                try
                {
                    Auth.SetUserCredentials("yourkey",
                        "yoursecret",
                        ConfigurationManager.AppSettings["AccessToken"],
                        ConfigurationManager.AppSettings["AccessTokenSecret"]);
                }
                catch (Exception)
                {
                    Console.WriteLine("Uh oh! Something went Wrong, Press a key to shutdown!");
                    Console.ReadKey();
                    Environment.Exit(0);
                }
            _client = new DiscordSocketClient();
            _commands = new CommandService();
            var token = ConfigurationManager.AppSettings["DiscordToken"];

            _services = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(_commands)
                .BuildServiceProvider();

            await InstallCommandsAsync();

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
            Console.WriteLine("Login(s) success");
            //var authenticatedUser = User.GetAuthenticatedUser();

            //Console.WriteLine("Tweet stream has started, press Enter to cancel");
            Console.ReadLine();
            if (manageScanner.stream != null)
                if (manageScanner.stream.StreamState == StreamState.Running) manageScanner.stream.StopStream();
            Console.WriteLine("Shutdown");
            //stream.StopStream();
        }

        public async Task InstallCommandsAsync()
        {
            // Hook the MessageReceived Event into our Command Handler
            _client.MessageReceived += HandleCommandAsync;
            // Discover all of the commands in this assembly and load them.
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly());
        }

        private async Task HandleCommandAsync(SocketMessage messageParam)
        {
            // Don't process the command if it was a System Message
            var message = messageParam as SocketUserMessage;
            if (message == null) return;
            // Create a number to track where the prefix ends and the command begins
            var argPos = 0;
            // Determine if the message is a command, based on if it starts with '!' or a mention prefix
            if (!(message.HasCharPrefix('!', ref argPos) || message.HasMentionPrefix(_client.CurrentUser, ref argPos)))
                return;
            // Create a Command Context
            var context = new SocketCommandContext(_client, message);
            // Execute the command. (result does not indicate a return value, 
            // rather an object stating if the command executed successfully)
            var result = await _commands.ExecuteAsync(context, argPos, _services);
            if (!result.IsSuccess)
                await context.Channel.SendMessageAsync(result.ErrorReason);
        }
    }

    [Group("scan")]
    public class scan : ModuleBase<SocketCommandContext>
    {
        [Command("start")]
        [Summary("start scanning for police alerts")]
        public async Task StartAsync()
        {
            if (!manageScanner.isRunning)
            {
                Console.WriteLine("Running scan...");
                manageScanner.stream = Stream.CreateFilteredStream();
                // We can also access the channel from the Command Context.
                await Context.Channel.SendMessageAsync("Haiii~ Lookin for trouble ε=ε=┏( >_<)┛");
                manageScanner.isRunning = true;
                var streamThread = new Thread(() =>
                {
                    manageScanner.stream.AddFollow(799242246);
                    manageScanner.stream.AddFollow(108746627);
                    manageScanner.stream.MatchingTweetReceived += (sender, args) =>
                    {
                        if (!args.Tweet.IsRetweet)
                        {
                            //Not really understanding what this does
                            var result = args.Tweet.FullText;
                            try
                            {
                                Console.WriteLine(result);
                            }
                            catch (Exception)
                            {
                                Console.WriteLine(
                                    "Oh crumbs the tweet couldn't be output to console, thats really weird :s");
                            }
                            var url = "";
                            var m = result.IndexOf("htt");
                            if (m != -1)
                            {
                                for (var i = m; (i <= result.Length - 1) && (result[i] != ' '); i++)
                                    url += result[i];
                                var creator = args.Tweet.CreatedBy.Name + "";
                                Context.Channel.SendMessageAsync(
                                    ":police_car: Bad guy alert ~~ Get em Oni-chaaaan! :police_car: " + url
                                    + " Arigato " + creator + " :heart:");
                            }
                        }
                    };
                    manageScanner.stream.StartStreamMatchingAnyCondition();
                });
                streamThread.Start();
                Console.WriteLine("scan Running");
            }
            else await Context.Channel.SendMessageAsync("Already Running Baka!");
        }

        [Command("end")]
        [Summary("stop scanning for police alerts")]
        public async Task endAsync()
        {
            if (manageScanner.isRunning)
            {
                Console.WriteLine("Ending scan...");
                manageScanner.stream.StopStream();
                await Context.Channel.SendMessageAsync("Stream stopped U_U");
                manageScanner.isRunning = false;
                Console.WriteLine("Scan end");
            }
            else await Context.Channel.SendMessageAsync("Not Running Baka!");
        }
    }

    [Group("Popochan")]
    public class Info : ModuleBase<SocketCommandContext>
    {
        // ~say hello -> hello
        [Command("say")]
        [Summary("Echos a message.")]
        public async Task SayAsync([Remainder] [Summary("The text to echo")] string echo)
        {
            // ReplyAsync is a method on ModuleBase
            await ReplyAsync(echo);
        }

        [Command("goodBot")]
        [Summary("Praise the bot")]
        public async Task goodBotAsync()
        {
            var botPointsInt = int.Parse(ConfigurationManager.AppSettings["botPoints"]);
            botPointsInt++;
            ConfigurationManager.AppSettings["botPoints"] = botPointsInt.ToString();

            await Context.Channel.SendMessageAsync("<3 ^_^ <3 Cookies: " + botPointsInt);
        }

        [Command("badBot")]
        [Summary("Scold the bot :(")]
        public async Task badBotAsync()
        {
            var botPointsInt = int.Parse(ConfigurationManager.AppSettings["botPoints"]);
            botPointsInt--;
            ConfigurationManager.AppSettings["botPoints"] = botPointsInt.ToString();
            await Context.Channel.SendMessageAsync("But...why? ;_; Cookies: " + botPointsInt);
        }
    }

    public class Crims : ModuleBase<SocketCommandContext>
    {
        [Command("criminals")]
        [Summary(">:(")]
        public async Task crimsAsync()
        {
            await Context.Channel.SendMessageAsync("https://www.youtube.com/watch?v=4zoLyXSno4s");
        }
    }

    [Group("music")]
    public class Music : ModuleBase<SocketCommandContext>
    {
        [Command("chase")]
        [Summary("DEJA VU")]
        public async Task crimsAsync()
        {
            await Context.Channel.SendMessageAsync("**DO YOU LIKE** " + "https://youtu.be/d25O7qQ3Sn4" + "** MY CAR**");
        }
    }

    //
}