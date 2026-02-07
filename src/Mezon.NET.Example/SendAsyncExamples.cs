//using Mezon.NET.Core;
//using Mezon.Protobuf.Api;
//using System;
//using System.Threading;
//using System.Threading.Tasks;

//namespace Mezon.NET.Example
//{
//    /// <summary>
//    /// Examples demonstrating the use of SendAsync methods in DefaultGRPCClient.
//    /// </summary>
//    public class SendAsyncExamples
//    {
//        private readonly string _apiUrl = "https://api.mezon.vn";

//        /// <summary>
//        /// Example 1: Basic authentication using SendAsync
//        /// </summary>
//        public async Task BasicAuthenticationExample()
//        {
//            Console.WriteLine("=== Example 1: Basic Authentication ===");

//            using var grpcClient = new DefaultGRPCClient(_apiUrl);

//            var request = new AccountEmail
//            {
//                Email = "user@example.com",
//                Password = "password123"
//            };

//            try
//            {
//                var response = await grpcClient.SendAsync(
//                    request,
//                    (req, opts) => grpcClient.Client.AuthenticateEmailAsync(req, opts)
//                );

//                Console.WriteLine($"? Authenticated successfully");
//                Console.WriteLine($"  Token: {response.Token[..20]}...");
//                Console.WriteLine($"  User ID: {response.UserId}");
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"? Authentication failed: {ex.Message}");
//            }
//        }

//        /// <summary>
//        /// Example 2: Call with timeout using custom cancellation token
//        /// </summary>
//        public async Task TimeoutExample()
//        {
//            Console.WriteLine("\n=== Example 2: Request with Timeout ===");

//            using var grpcClient = new DefaultGRPCClient(_apiUrl);
//            grpcClient.SetHeader("Authorization", "Bearer sample-token");

//            var request = new ListClanDescRequest
//            {
//                Limit = 10
//            };

//            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

//            try
//            {
//                var response = await grpcClient.SendAsync(
//                    request,
//                    (req, opts) => grpcClient.Client.ListClanDescsAsync(req, opts),
//                    cts.Token // Override with timeout token
//                );

//                Console.WriteLine($"? Retrieved {response.ClanDescs.Count} clans");
//            }
//            catch (OperationCanceledException)
//            {
//                Console.WriteLine("? Request timed out after 5 seconds");
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"? Error: {ex.Message}");
//            }
//        }

//        /// <summary>
//        /// Example 3: Create clan with headers and default cancellation
//        /// </summary>
//        public async Task CreateClanExample()
//        {
//            Console.WriteLine("\n=== Example 3: Create Clan ===");

//            using var grpcClient = new DefaultGRPCClient(_apiUrl);

//            // Set authentication
//            grpcClient.SetHeader("Authorization", "Bearer session-token");
//            grpcClient.SetHeader("x-app-version", "1.0.0");

//            // Set default cancellation for all calls
//            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
//            grpcClient.SetCancelToken(cts.Token);

//            var request = new CreateClanDescRequest
//            {
//                CreatorId = "user-123",
//                ClanName = "My Gaming Clan",
//                Logo = "https://example.com/logo.png"
//            };

//            try
//            {
//                var response = await grpcClient.SendAsync(
//                    request,
//                    (req, opts) => grpcClient.Client.CreateClanAsync(req, opts)
//                );

//                Console.WriteLine($"? Clan created successfully");
//                Console.WriteLine($"  Clan ID: {response.ClanId}");
//                Console.WriteLine($"  Name: {response.ClanName}");
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"? Failed to create clan: {ex.Message}");
//            }
//        }

//        /// <summary>
//        /// Example 4: Complete workflow - Create clan, category, and channel
//        /// </summary>
//        public async Task CompleteWorkflowExample()
//        {
//            Console.WriteLine("\n=== Example 4: Complete Workflow ===");

//            using var grpcClient = new DefaultGRPCClient(_apiUrl);
//            grpcClient.SetHeader("Authorization", "Bearer session-token");

//            try
//            {
//                // Step 1: Create Clan
//                Console.WriteLine("Step 1: Creating clan...");
//                var clanRequest = new CreateClanDescRequest
//                {
//                    ClanName = "Test Clan",
//                    CreatorId = "user-123"
//                };

//                var clan = await grpcClient.SendAsync(
//                    clanRequest,
//                    (req, opts) => grpcClient.Client.CreateClanAsync(req, opts)
//                );
//                Console.WriteLine($"? Clan created: {clan.ClanId}");

//                // Step 2: Create Category
//                Console.WriteLine("Step 2: Creating category...");
//                var categoryRequest = new CreateCategoryDescRequest
//                {
//                    ClanId = clan.ClanId,
//                    CategoryName = "General",
//                    CreatorId = "user-123"
//                };

//                var category = await grpcClient.SendAsync(
//                    categoryRequest,
//                    (req, opts) => grpcClient.Client.CreateCategoryAsync(req, opts)
//                );
//                Console.WriteLine($"? Category created: {category.CategoryId}");

//                // Step 3: Create Channel
//                Console.WriteLine("Step 3: Creating channel...");
//                var channelRequest = new CreateChannelDescRequest
//                {
//                    ClanId = clan.ClanId,
//                    CategoryId = category.CategoryId,
//                    ChannelLabel = "general-chat",
//                    Type = 1,
//                    CreatorId = "user-123"
//                };

//                var channel = await grpcClient.SendAsync(
//                    channelRequest,
//                    (req, opts) => grpcClient.Client.CreateChannelDescAsync(req, opts)
//                );
//                Console.WriteLine($"? Channel created: {channel.ChannelId}");

//                Console.WriteLine("\n? Complete workflow finished successfully!");
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"? Workflow failed: {ex.Message}");
//            }
//        }

//        /// <summary>
//        /// Example 5: Handling different error scenarios
//        /// </summary>
//        public async Task ErrorHandlingExample()
//        {
//            Console.WriteLine("\n=== Example 5: Error Handling ===");

//            using var grpcClient = new DefaultGRPCClient(_apiUrl);

//            var request = new AccountEmail
//            {
//                Email = "invalid@example.com",
//                Password = "wrong-password"
//            };

//            try
//            {
//                var response = await grpcClient.SendAsync(
//                    request,
//                    (req, opts) => grpcClient.Client.AuthenticateEmailAsync(req, opts)
//                );

//                Console.WriteLine("? Authentication successful (unexpected)");
//            }
//            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Unauthenticated)
//            {
//                Console.WriteLine("? Authentication failed: Invalid credentials");
//            }
//            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Unavailable)
//            {
//                Console.WriteLine("? Service unavailable: Check your connection");
//            }
//            catch (Grpc.Core.RpcException ex)
//            {
//                Console.WriteLine($"? gRPC Error [{ex.StatusCode}]: {ex.Status.Detail}");
//            }
//            catch (OperationCanceledException)
//            {
//                Console.WriteLine("? Operation was cancelled");
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"? Unexpected error: {ex.Message}");
//            }
//        }

//        /// <summary>
//        /// Example 6: Token refresh pattern
//        /// </summary>
//        public async Task TokenRefreshExample()
//        {
//            Console.WriteLine("\n=== Example 6: Token Refresh Pattern ===");

//            using var grpcClient = new DefaultGRPCClient(_apiUrl);

//            try
//            {
//                // Initial authentication
//                Console.WriteLine("Step 1: Initial authentication...");
//                var authRequest = new AccountEmail
//                {
//                    Email = "user@example.com",
//                    Password = "password123"
//                };

//                var session = await grpcClient.SendAsync(
//                    authRequest,
//                    (req, opts) => grpcClient.Client.AuthenticateEmailAsync(req, opts)
//                );

//                Console.WriteLine("? Initial authentication successful");
//                grpcClient.SetHeader("Authorization", $"Bearer {session.Token}");

//                // Make some authenticated calls
//                Console.WriteLine("Step 2: Making authenticated request...");
//                var listRequest = new ListClanDescRequest { Limit = 5 };

//                var clans = await grpcClient.SendAsync(
//                    listRequest,
//                    (req, opts) => grpcClient.Client.ListClanDescsAsync(req, opts)
//                );

//                Console.WriteLine($"? Retrieved {clans.ClanDescs.Count} clans");

//                // Simulate token expiration - refresh token
//                Console.WriteLine("Step 3: Refreshing session token...");
//                var refreshRequest = new SessionRefreshRequest
//                {
//                    Token = session.RefreshToken
//                };

//                var newSession = await grpcClient.SendAsync(
//                    refreshRequest,
//                    (req, opts) => grpcClient.Client.SessionRefreshAsync(req, opts)
//                );

//                Console.WriteLine("? Token refreshed successfully");
//                grpcClient.SetHeader("Authorization", $"Bearer {newSession.Token}");

//                // Continue making calls with new token
//                Console.WriteLine("Step 4: Making request with new token...");
//                var clans2 = await grpcClient.SendAsync(
//                    listRequest,
//                    (req, opts) => grpcClient.Client.ListClanDescsAsync(req, opts)
//                );

//                Console.WriteLine($"? Retrieved {clans2.ClanDescs.Count} clans with refreshed token");
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"? Token refresh workflow failed: {ex.Message}");
//            }
//        }

//        /// <summary>
//        /// Example 7: Parallel requests with shared configuration
//        /// </summary>
//        public async Task ParallelRequestsExample()
//        {
//            Console.WriteLine("\n=== Example 7: Parallel Requests ===");

//            using var grpcClient = new DefaultGRPCClient(_apiUrl);
//            grpcClient.SetHeader("Authorization", "Bearer session-token");

//            var clanIds = new[] { "clan-1", "clan-2", "clan-3" };

//            try
//            {
//                Console.WriteLine($"Fetching {clanIds.Length} clan profiles in parallel...");

//                var tasks = new Task<ClanProfile>[clanIds.Length];
//                for (int i = 0; i < clanIds.Length; i++)
//                {
//                    var clanId = clanIds[i];
//                    var request = new ClanProfileRequest { ClanId = clanId };

//                    tasks[i] = grpcClient.SendAsync(
//                        request,
//                        (req, opts) => grpcClient.Client.GetClanProfileAsync(req, opts)
//                    );
//                }

//                var profiles = await Task.WhenAll(tasks);

//                Console.WriteLine($"? Retrieved {profiles.Length} clan profiles");
//                foreach (var profile in profiles)
//                {
//                    Console.WriteLine($"  - {profile.ClanName} (Members: {profile.MemberCount})");
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"? Parallel requests failed: {ex.Message}");
//            }
//        }

//        /// <summary>
//        /// Example 8: Comparing traditional vs SendAsync approaches
//        /// </summary>
//        public async Task ComparisonExample()
//        {
//            Console.WriteLine("\n=== Example 8: Traditional vs SendAsync Comparison ===");

//            using var grpcClient = new DefaultGRPCClient(_apiUrl);
//            grpcClient.SetHeader("Authorization", "Bearer token");

//            var request = new ListClanDescRequest { Limit = 5 };

//            // Traditional approach
//            Console.WriteLine("\nTraditional approach:");
//            try
//            {
//                var callOptions = grpcClient.GetCallOptions();
//                var response1 = await grpcClient.Client.ListClanDescsAsync(request, callOptions);
//                Console.WriteLine($"? Retrieved {response1.ClanDescs.Count} clans (traditional)");
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"? Traditional approach failed: {ex.Message}");
//            }

//            // SendAsync approach
//            Console.WriteLine("\nSendAsync approach:");
//            try
//            {
//                var response2 = await grpcClient.SendAsync(
//                    request,
//                    (req, opts) => grpcClient.Client.ListClanDescsAsync(req, opts)
//                );
//                Console.WriteLine($"? Retrieved {response2.ClanDescs.Count} clans (SendAsync)");
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"? SendAsync approach failed: {ex.Message}");
//            }

//            Console.WriteLine("\nBoth approaches produce identical results!");
//            Console.WriteLine("SendAsync is more concise and handles CallOptions automatically.");
//        }

//        /// <summary>
//        /// Run all examples
//        /// </summary>
//        public async Task RunAllExamples()
//        {
//            Console.WriteLine("??????????????????????????????????????????????????");
//            Console.WriteLine("?   gRPC SendAsync Method Examples              ?");
//            Console.WriteLine("??????????????????????????????????????????????????\n");

//            await BasicAuthenticationExample();
//            await TimeoutExample();
//            await CreateClanExample();
//            await CompleteWorkflowExample();
//            await ErrorHandlingExample();
//            await TokenRefreshExample();
//            await ParallelRequestsExample();
//            await ComparisonExample();

//            Console.WriteLine("\n??????????????????????????????????????????????????");
//            Console.WriteLine("?   All Examples Completed                      ?");
//            Console.WriteLine("??????????????????????????????????????????????????");
//        }
//    }
//}
