using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Storage;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🔍 Azure Subscription Scanner - Storage Accounts & Blobs\n");

        try
        {
            // Initialize Azure clients - Try multiple credential types
            Console.WriteLine("🔐 Authenticating with Azure...\n");
            
            TokenCredential credential;
            
            try
            {
                // First try DefaultAzureCredential (includes az login, env vars, etc.)
                credential = new DefaultAzureCredential();
                Console.WriteLine("✅ Using DefaultAzureCredential (Azure CLI / Environment)\n");
            }
            catch
            {
                // Fallback to interactive browser
                Console.WriteLine("⚠️  DefaultAzureCredential failed, using Interactive Browser...\n");
                credential = new InteractiveBrowserCredential();
            }
            
            var client = new ArmClient(credential);
            
            // Ask user for subscription ID
            Console.WriteLine("Enter Subscription ID (or press Enter to show all subscriptions):");
            string? selectedSubscriptionId = Console.ReadLine();
            
            // If no input, list all subscriptions to help user choose
            if (string.IsNullOrWhiteSpace(selectedSubscriptionId))
            {
                Console.WriteLine("\n📋 Available subscriptions:\n");
                await foreach (var sub in client.GetSubscriptions().GetAllAsync())
                {
                    Console.WriteLine($"   {sub.Data.SubscriptionId} - {sub.Data.DisplayName}");
                }
                Console.WriteLine("\nPlease run the program again and enter a Subscription ID from the list above.");
                return;
            }
            
            selectedSubscriptionId = selectedSubscriptionId.Trim();
            
            // Output file path - save in sepia directory, not in bin
            string outputPath = Path.Combine(Directory.GetCurrentDirectory(), "azure_scan_results.pdf");

            using (PdfWriter pdfWriter = new PdfWriter(outputPath))
            using (PdfDocument pdfDoc = new PdfDocument(pdfWriter))
            using (Document doc = new Document(pdfDoc))
            {
                doc.Add(new Paragraph("=== Azure Subscription Scan Results ===").SetFontSize(16).SetBold());
                doc.Add(new Paragraph($"Scanned at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}").SetFontSize(10));
                doc.Add(new Paragraph("Format: SubscriptionID | Name | Containers").SetFontSize(10));
                doc.Add(new Paragraph(" "));

                int subscriptionCount = 0;
                int storageAccountCount = 0;
                int containerCount = 0;
                int fileShareCount = 0;

                try
                {
                    // Get the selected subscription
                    var subscriptionResource = client.GetSubscriptionResource(new Azure.Core.ResourceIdentifier($"/subscriptions/{selectedSubscriptionId}"));
                    var subscriptionResponse = await subscriptionResource.GetAsync();
                    var subscription = subscriptionResponse.Value;
                    
                    if (subscription == null)
                    {
                        Console.WriteLine($"❌ Subscription not found: {selectedSubscriptionId}");
                        return;
                    }

                    subscriptionCount++;
                    var subscriptionId = subscription.Data.SubscriptionId;
                    var subscriptionName = subscription.Data.DisplayName ?? "N/A";
                    Console.WriteLine($"📋 Subscription: {subscriptionId} - {subscriptionName}\n");
                    doc.Add(new Paragraph($"\n{subscriptionId} | {subscriptionName}").SetBold());

                    try
                    {
                        await foreach (var resourceGroup in subscription.GetResourceGroups().GetAllAsync())
                        {
                            var storageAccounts = resourceGroup.GetStorageAccounts();
                            
                            await foreach (var storageAccount in storageAccounts.GetAllAsync())
                            {
                                storageAccountCount++;
                                string accountName = storageAccount.Data.Name;
                                var resourceGroupName = resourceGroup.Data.Name;
                                var location = storageAccount.Data.Location;
                                var kind = storageAccount.Data.Kind;
                                var skuName = storageAccount.Data.Sku?.Name;
                                var accessTier = storageAccount.Data.AccessTier;
                                var createdTime = storageAccount.Data.SystemData?.CreatedOn;
                                var primaryEndpoints = storageAccount.Data.PrimaryEndpoints;
                                
                                Console.WriteLine($"  💾 Storage Account: {accountName}");
                                Console.WriteLine($"     Location: {location}, Kind: {kind}, Tier: {accessTier}");

                                try
                                {
                                    doc.Add(new Paragraph($"Storage Account: {accountName}").SetBold().SetMarginLeft(10));
                                    doc.Add(new Paragraph($"Resource Group: {resourceGroupName}").SetMarginLeft(20));
                                    doc.Add(new Paragraph($"Location: {location}").SetMarginLeft(20));
                                    doc.Add(new Paragraph($"Kind: {kind}").SetMarginLeft(20));
                                    doc.Add(new Paragraph($"SKU: {skuName}").SetMarginLeft(20));
                                    doc.Add(new Paragraph($"Access Tier: {accessTier}").SetMarginLeft(20));
                                    doc.Add(new Paragraph($"Created: {(createdTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A")}").SetMarginLeft(20));
                                    if (primaryEndpoints != null)
                                    {
                                        if (primaryEndpoints.BlobUri != null)
                                            doc.Add(new Paragraph($"Blob Endpoint: {primaryEndpoints.BlobUri.AbsoluteUri}").SetMarginLeft(20));
                                        if (primaryEndpoints.FileUri != null)
                                            doc.Add(new Paragraph($"File Endpoint: {primaryEndpoints.FileUri.AbsoluteUri}").SetMarginLeft(20));
                                        if (primaryEndpoints.QueueUri != null)
                                            doc.Add(new Paragraph($"Queue Endpoint: {primaryEndpoints.QueueUri.AbsoluteUri}").SetMarginLeft(20));
                                        if (primaryEndpoints.TableUri != null)
                                            doc.Add(new Paragraph($"Table Endpoint: {primaryEndpoints.TableUri.AbsoluteUri}").SetMarginLeft(20));
                                    }
                                    
                                    var blobServiceClient = new BlobServiceClient(
                                        new Uri($"https://{accountName}.blob.core.windows.net"),
                                        credential
                                    );

                                    doc.Add(new Paragraph($"Blob Containers:").SetBold().SetMarginLeft(20));
                                    
                                    await foreach (var container in blobServiceClient.GetBlobContainersAsync())
                                    {
                                        containerCount++;
                                        Console.WriteLine($"    📁 Blob Container: {container.Name}");
                                        doc.Add(new Paragraph($"{container.Name}").SetMarginLeft(30));
                                    }

                                    // List File Shares using storage account keys
                                    try
                                    {
                                        string? storageKey = null;
                                        
                                        // Get the storage account key
                                        await foreach (var key in storageAccount.GetKeysAsync())
                                        {
                                            storageKey = key.Value;
                                            break; // We only need the first key
                                        }
                                        
                                        if (!string.IsNullOrEmpty(storageKey))
                                        {
                                            var fileServiceClient = new ShareServiceClient(
                                                new Uri($"https://{accountName}.file.core.windows.net"),
                                                new StorageSharedKeyCredential(accountName, storageKey)
                                            );

                                            doc.Add(new Paragraph($"File Shares:").SetBold().SetMarginLeft(20));
                                            
                                            await foreach (var share in fileServiceClient.GetSharesAsync())
                                            {
                                                fileShareCount++;
                                                Console.WriteLine($"    📂 File Share: {share.Name}");
                                                doc.Add(new Paragraph($"{share.Name}").SetMarginLeft(30));
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"    ⚠️  Could not access file shares: {ex.Message}");
                                        doc.Add(new Paragraph($"File Shares Error: {ex.Message}").SetMarginLeft(20));
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"    ⚠️  Could not access blobs: {ex.Message}");
                                    doc.Add(new Paragraph($"Error: {ex.Message}").SetMarginLeft(20));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ⚠️  Error processing subscription: {ex.Message}");
                        doc.Add(new Paragraph($"Error: {ex.Message}"));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error getting subscription: {ex.Message}");
                    doc.Add(new Paragraph($"❌ Error: {ex.Message}").SetBold());
                }

                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph($"Summary: {subscriptionCount} subscriptions, {storageAccountCount} storage accounts, {containerCount} blob containers, {fileShareCount} file shares").SetBold());
                doc.Add(new Paragraph("=== Scan Complete ===").SetBold());
            }

            Console.WriteLine($"\n✅ Scan complete! Results saved to: {outputPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine($"Details: {ex.StackTrace}");
        }
    }
}
