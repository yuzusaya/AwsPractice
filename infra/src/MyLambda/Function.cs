using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Model;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace MyLambda
{
    public class Function
    {
        private static readonly AmazonDynamoDBClient DynamoDbClient = new AmazonDynamoDBClient();
        private static readonly AmazonS3Client S3Client = new AmazonS3Client();

        public async Task FunctionHandler(S3Event evnt, ILambdaContext context)
        {
            var currentTime = DateTimeOffset.UtcNow;
            var japanTime = TimeZoneInfo.ConvertTime(currentTime, TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time"));
            context.Logger.LogInformation($"Processing {evnt.Records.Count} records...");
            foreach (var record in evnt.Records)
            {
                try
                {
                    var size = record.S3.Object.Size;
                    var bucketName = record.S3.Bucket.Name;
                    var objectKey = record.S3.Object.Key;

                    var metadata = new Dictionary<string, AttributeValue>
                    {
                        { "FilePath", new AttributeValue { S = objectKey } },
                        { "BucketName", new AttributeValue { S = bucketName } },
                        { "Timestamp", new AttributeValue { S = japanTime.ToString("o") } },
                        { "Size", new AttributeValue { N = size.ToString() } }
                    };

                    // Resize and replace the image if it's larger than 1MB
                    if (size > 1 * 1024 * 1024)
                    {
                        int targetWidth = 128;
                        int targetHeight = 128;
                        var resizedImage = await ResizeImage(bucketName, objectKey, targetWidth, targetHeight);
                        await UploadResizedImage(bucketName, objectKey, resizedImage);
                        metadata.Add("NewSize", new AttributeValue { N = resizedImage.Length.ToString() });
                    }
                    await DynamoDbClient.PutItemAsync(Environment.GetEnvironmentVariable("TABLE_NAME"), metadata);
                }
                catch (Exception e)
                {
                    context.Logger.LogError($"Error: {e.Message}");
                }

            }
        }

        /// <summary>
        /// Resize an image from S3 to the specified width and height.
        /// </summary>
        /// <param name="bucketName">The S3 bucket name</param>
        /// <param name="objectKey">The S3 object key (file path)</param>
        /// <param name="width">Target width</param>
        /// <param name="height">Target height</param>
        /// <returns>A MemoryStream containing the resized image</returns>
        public async Task<MemoryStream> ResizeImage(string bucketName, string objectKey, int width, int height)
        {
            // Retrieve the image from S3
            var getObjectResponse = await S3Client.GetObjectAsync(bucketName, objectKey);
            using (var inputStream = getObjectResponse.ResponseStream)
            {
                // Load the image from the stream
                using (Image image = Image.Load(inputStream))
                {
                    // Resize the image
                    image.Mutate(x => x.Resize(width, height));

                    // Save the resized image to a MemoryStream
                    var memoryStream = new MemoryStream();
                    image.SaveAsJpeg(memoryStream);  // Save as JPEG (you can use other formats like PNG if needed)
                    memoryStream.Position = 0;  // Reset stream position for later upload

                    return memoryStream;
                }
            }
        }

        /// <summary>
        /// Upload the resized image back to S3, replacing the original image.
        /// </summary>
        /// <param name="bucketName">The S3 bucket name</param>
        /// <param name="objectKey">The S3 object key (file path)</param>
        /// <param name="resizedImage">The MemoryStream containing the resized image</param>
        /// <returns>A Task representing the upload operation</returns>
        public async Task UploadResizedImage(string bucketName, string objectKey, MemoryStream resizedImage)
        {
            // Create a PutObjectRequest to upload the resized image back to S3
            var putRequest = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = objectKey,
                InputStream = resizedImage,
                ContentType = "image/jpeg"  // Change if you're using a different format
            };

            // Upload the resized image
            await S3Client.PutObjectAsync(putRequest);
        }
    }
}