using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace MyLambda
{
    public class Function
    {
        private static readonly AmazonDynamoDBClient DynamoDbClient = new AmazonDynamoDBClient();

        public async Task FunctionHandler(S3Event evnt, ILambdaContext context)
        {
            var currentTime = DateTimeOffset.UtcNow;
            var japanTime = TimeZoneInfo.ConvertTime(currentTime, TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time"));
            context.Logger.LogInformation($"Processing {evnt.Records.Count} records...");
            foreach (var record in evnt.Records)
            {
                try
                {
                    var bucketName = record.S3.Bucket.Name;
                    var objectKey = record.S3.Object.Key;

                    var metadata = new Dictionary<string, AttributeValue>
                        {
                            { "FilePath", new AttributeValue { S = objectKey } },
                            { "BucketName", new AttributeValue { S = bucketName } },
                            { "Timestamp", new AttributeValue { S = japanTime.ToString("o") } }
                        };
                    await DynamoDbClient.PutItemAsync(Environment.GetEnvironmentVariable("TABLE_NAME"), metadata);
                }
                catch (Exception e)
                {
                    context.Logger.LogError($"Error: {e.Message}");
                }

            }
        }
    }
}