//using System;
//using System.Threading;
//using System.Threading.Tasks;
//using Grpc.Core;
//using Mezon.NET.Core;
//using Mezon.NET.Abstractions;
//using Google.Protobuf.WellKnownTypes;

//namespace Mezon.NET.Example
//{
//    /// <summary>
//    /// Example demonstrating the usage of SetHeader and SetCancelToken methods
//    /// in the DefaultGRPCClient implementation.
//    /// </summary>
//    public class GrpcClientExample
//    {
//        private readonly string _apiUrl = "https://api.mezon.ai";
//        private readonly string _authToken = "your-jwt-token-here";

//        /// <summary>
//        /// Example 1: Basic usage with authentication header
//        /// </summary>
//        public async Task BasicAuthenticationExample()
//        {
//            Console.WriteLine("=== Example 1: Basic Authentication ===");

//            using var grpcClient = new DefaultGRPCClient(_apiUrl);

//            // Set authentication header
//            grpcClient.SetHeader("authorization", $"Bearer {_authToken}");

//            var client = grpcClient.Client;
//            var callOptions = grpcClient.GetCallOptions();

//            try
//            {
//                // Get current user account
//                var account = await client.GetAccountAsync(new Empty(), callOptions);
//                Console.WriteLine($"Authenticated as: {account.User.Username}");
//                Console.WriteLine($"User ID: {account.User.Id}");
//            }
//            catch (RpcException ex)
//            {
//                Console.WriteLine($"Error: {ex.Status.Detail}");
//            }
//        }

//        /// <summary>
//        /// Example 2: Using cancellation token for timeout
//        /// </summary>
//        public async Task TimeoutExample()
//        {
//            Console.WriteLine("\n=== Example 2: Request Timeout ===");

//            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
//            using var grpcClient = new DefaultGRPCClient(_apiUrl);

//            // Set auth header and cancellation token
//            grpcClient.SetHeader("authorization", $"Bearer {_authToken}");
//            grpcClient.SetCancelToken(cts.Token);

//            var client = grpcClient.Client;
//            var callOptions = grpcClient.GetCallOptions();

//            try
//            {
//                // List clans with 30-second timeout
//                var clans = await client.ListClanDescsAsync(
//                    new ListClanDescRequest { Limit = 100 },
//                    callOptions
//                );

//                Console.WriteLine($"Found {clans.Clandesc.Count} clans");
//                foreach (var clan in clans.Clandesc)
//                {
//                    Console.WriteLine($"  - {clan.ClanName} (ID: {clan.ClanId})");
//                }
//            }
//            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
//            {
//                Console.WriteLine("Request was cancelled");
//            }
//            catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
//            {
//                Console.WriteLine("Request timeout after 30 seconds");
//            }
//            catch (RpcException ex)
//            {
//                Console.WriteLine($"Error: {ex.Status.Detail}");
//            }
//        }

//        /// <summary>
//        /// Example 3: Multiple headers for API key and request tracking
//        /// </summary>
//        public async Task MultipleHeadersExample()
//        {
//            Console.WriteLine("\n=== Example 3: Multiple Headers ===");

//            using var grpcClient = new DefaultGRPCClient(_apiUrl);

//            // Set multiple headers
//            grpcClient.SetHeader("authorization", $"Bearer {_authToken}");
//            grpcClient.SetHeader("x-api-key", "your-api-key");
//            grpcClient.SetHeader("x-request-id", Guid.NewGuid().ToString());
//            grpcClient.SetHeader("x-client-version", "1.0.0");

//            var client = grpcClient.Client;
//            var callOptions = grpcClient.GetCallOptions();

//            try
//            {
//                var account = await client.GetAccountAsync(new Empty(), callOptions);
//                Console.WriteLine($"Request successful for user: {account.User.Username}");
//            }
//            catch (RpcException ex)
//            {
//                Console.WriteLine($"Error: {ex.Status.Detail}");
//            }
//        }

//        /// <summary>
//        /// Example 4: Manual cancellation during operation
//        /// </summary>
//        public async Task ManualCancellationExample()
//        {
//            Console.WriteLine("\n=== Example 4: Manual Cancellation ===");

//            var cts = new CancellationTokenSource();
//            using var grpcClient = new DefaultGRPCClient(_apiUrl);

//            grpcClient.SetHeader("authorization", $"Bearer {_authToken}");
//            grpcClient.SetCancelToken(cts.Token);

//            var client = grpcClient.Client;
//            var callOptions = grpcClient.GetCallOptions();

//            // Start a long-running operation
//            var task = client.ListClanUsersAsync(
//                new ListClanUsersRequest { ClanId = 12345 },
//                callOptions
//            );

//            // Simulate user cancellation after 2 seconds
//            _ = Task.Run(async () =>
//            {
//                await Task.Delay(2000);
//                Console.WriteLine("Cancelling operation...");
//                cts.Cancel();
//            });

//            try
//            {
//                var users = await task;
//                Console.WriteLine($"Retrieved {users.ClanUsers.Count} users");
//            }
//            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
//            {
//                Console.WriteLine("Operation was cancelled by user");
//            }
//            catch (RpcException ex)
//            {
//                Console.WriteLine($"Error: {ex.Status.Detail}");
//            }
//        }

//        /// <summary>
//        /// Example 5: Complete workflow with clan and channel operations
//        /// </summary>
//        public async Task CompleteWorkflowExample()
//        {
//            Console.WriteLine("\n=== Example 5: Complete Workflow ===");

//            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
//            using var grpcClient = new DefaultGRPCClient(_apiUrl);

//            // Configure client
//            grpcClient.SetHeader("authorization", $"Bearer {_authToken}");
//            grpcClient.SetHeader("x-request-id", Guid.NewGuid().ToString());
//            grpcClient.SetCancelToken(cts.Token);

//            var client = grpcClient.Client;
//            var callOptions = grpcClient.GetCallOptions();

//            try
//            {
//                // 1. Get account information
//                Console.WriteLine("Step 1: Getting account info...");
//                var account = await client.GetAccountAsync(new Empty(), callOptions);
//                Console.WriteLine($"  User: {account.User.Username}");

//                // 2. Create a new clan
//                Console.WriteLine("\nStep 2: Creating new clan...");
//                var createClanRequest = new CreateClanDescRequest
//                {
//                    ClanName = "My Test Clan",
//                    Logo = "https://example.com/logo.png",
//                    Banner = "https://example.com/banner.png"
//                };
//                var clan = await client.CreateClanDescAsync(createClanRequest, callOptions);
//                Console.WriteLine($"  Created clan: {clan.ClanName} (ID: {clan.ClanId})");

//                // 3. Create a channel in the clan
//                Console.WriteLine("\nStep 3: Creating channel...");
//                var createChannelRequest = new CreateChannelDescRequest
//                {
//                    ClanId = clan.ClanId,
//                    ChannelLabel = "general",
//                    Type = 1, // Text channel
//                    ChannelPrivate = 0 // Public channel
//                };
//                var channel = await client.CreateChannelDescAsync(createChannelRequest, callOptions);
//                Console.WriteLine($"  Created channel: {channel.ChannelLabel} (ID: {channel.ChannelId})");

//                // 4. List channels in the clan
//                Console.WriteLine("\nStep 4: Listing channels...");
//                var channelsRequest = new ListChannelDescsRequest
//                {
//                    ClanId = clan.ClanId,
//                    Limit = 50
//                };
//                var channels = await client.ListChannelDescsAsync(channelsRequest, callOptions);
//                Console.WriteLine($"  Found {channels.Channeldesc.Count} channels");

//                // 5. Get messages from the channel
//                Console.WriteLine("\nStep 5: Getting messages...");
//                var messagesRequest = new ListChannelMessagesRequest
//                {
//                    ClanId = clan.ClanId,
//                    ChannelId = channel.ChannelId,
//                    Limit = 20
//                };
//                var messages = await client.ListChannelMessagesAsync(messagesRequest, callOptions);
//                Console.WriteLine($"  Retrieved {messages.Messages.Count} messages");

//                Console.WriteLine("\nWorkflow completed successfully!");
//            }
//            catch (RpcException ex)
//            {
//                Console.WriteLine($"\nWorkflow failed: {ex.Status.Detail}");
//            }
//        }

//        /// <summary>
//        /// Example 6: Updating headers dynamically (token refresh)
//        /// </summary>
//        public async Task DynamicHeaderUpdateExample()
//        {
//            Console.WriteLine("\n=== Example 6: Dynamic Header Updates ===");

//            using var grpcClient = new DefaultGRPCClient(_apiUrl);

//            // Initial authentication
//            string initialToken = "initial-token";
//            grpcClient.SetHeader("authorization", $"Bearer {initialToken}");

//            var client = grpcClient.Client;

//            try
//            {
//                // First request with initial token
//                Console.WriteLine("Making request with initial token...");
//                var callOptions1 = grpcClient.GetCallOptions();
//                var account1 = await client.GetAccountAsync(new Empty(), callOptions1);
//                Console.WriteLine($"  Success: {account1.User.Username}");

//                // Simulate token refresh
//                Console.WriteLine("\nRefreshing token...");
//                string refreshedToken = "refreshed-token";
//                grpcClient.SetHeader("authorization", $"Bearer {refreshedToken}");

//                // Second request with refreshed token
//                Console.WriteLine("Making request with refreshed token...");
//                var callOptions2 = grpcClient.GetCallOptions();
//                var account2 = await client.GetAccountAsync(new Empty(), callOptions2);
//                Console.WriteLine($"  Success: {account2.User.Username}");
//            }
//            catch (RpcException ex)
//            {
//                Console.WriteLine($"Error: {ex.Status.Detail}");
//            }
//        }

//        /// <summary>
//        /// Example 7: Parallel requests with shared headers
//        /// </summary>
//        public async Task ParallelRequestsExample()
//        {
//            Console.WriteLine("\n=== Example 7: Parallel Requests ===");

//            using var grpcClient = new DefaultGRPCClient(_apiUrl);

//            grpcClient.SetHeader("authorization", $"Bearer {_authToken}");
//            grpcClient.SetCancelToken(CancellationToken.None);

//            var client = grpcClient.Client;
//            var callOptions = grpcClient.GetCallOptions();

//            try
//            {
//                Console.WriteLine("Executing parallel requests...");

//                // Execute multiple requests in parallel
//                var accountTask = client.GetAccountAsync(new Empty(), callOptions);
//                var clansTask = client.ListClanDescsAsync(
//                    new ListClanDescRequest { Limit = 50 },
//                    callOptions
//                );
//                var friendsTask = client.ListFriendsAsync(
//                    new ListFriendsRequest { Limit = 100 },
//                    callOptions
//                );

//                // Wait for all to complete
//                await Task.WhenAll(accountTask, clansTask, friendsTask);

//                var account = await accountTask;
//                var clans = await clansTask;
//                var friends = await friendsTask;

//                Console.WriteLine($"Results:");
//                Console.WriteLine($"  Account: {account.User.Username}");
//                Console.WriteLine($"  Clans: {clans.Clandesc.Count}");
//                Console.WriteLine($"  Friends: {friends.Friends.Count}");
//            }
//            catch (RpcException ex)
//            {
//                Console.WriteLine($"Error: {ex.Status.Detail}");
//            }
//        }

//        /// <summary>
//        /// Run all examples
//        /// </summary>
//        public async Task RunAllExamples()
//        {
//            Console.WriteLine("===========================================");
//            Console.WriteLine("gRPC Client Examples");
//            Console.WriteLine("===========================================");

//            try
//            {
//                await BasicAuthenticationExample();
//                await TimeoutExample();
//                await MultipleHeadersExample();
//                await ManualCancellationExample();
//                await CompleteWorkflowExample();
//                await DynamicHeaderUpdateExample();
//                await ParallelRequestsExample();
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"\nUnexpected error: {ex.Message}");
//            }

//            Console.WriteLine("\n===========================================");
//            Console.WriteLine("All examples completed");
//            Console.WriteLine("===========================================");
//        }
//    }

//    // Example program entry point
//    public class Program
//    {
//        public static async Task Main(string[] args)
//        {
//            var examples = new GrpcClientExample();
//            await examples.RunAllExamples();
//        }
//    }
//}
