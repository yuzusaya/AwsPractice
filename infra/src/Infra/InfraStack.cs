using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3.Notifications;
using Constructs;

namespace Infra
{
    public class InfraStack : Stack
    {
        internal InfraStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            // The code that defines your stack goes here
            // S3 Bucket
            var bucket = new Bucket(this, "MyBucket", new BucketProps
            {
                RemovalPolicy = RemovalPolicy.DESTROY, // For testing, deletes the bucket on stack deletion
            });

            // DynamoDB Table
            var table = new Table(this, "MyTable", new TableProps
            {
                PartitionKey = new Attribute
                {
                    Name = "FilePath",
                    Type = AttributeType.STRING
                },
                RemovalPolicy = RemovalPolicy.DESTROY // For testing, deletes the table on stack deletion
            });

            // Lambda Function
            var lambdaFunction = new Function(this, "MyLambda", new FunctionProps
            {
                Architecture = Architecture.ARM_64, // Adjust for your preferred architecture
                Runtime = Runtime.DOTNET_8, // Adjust for your preferred runtime
                Code = Code.FromAsset("src/MyLambda/bin/Release/net8.0/publish"),
                Handler = "MyLambda::MyLambda.Function::FunctionHandler",
                Environment = new Dictionary<string, string>
                {
                    { "TABLE_NAME", table.TableName }
                },
                Timeout = Duration.Seconds(5 * 60)
            });
            // Grant Lambda function read access to objects in the "img/" folder of the S3 bucket
            bucket.GrantRead(lambdaFunction, "img/*");

            // Grant Lambda function write access to objects in the "img/" folder of the S3 bucket
            bucket.GrantPut(lambdaFunction, "img/*");

            // Grant Lambda permissions to write to DynamoDB
            table.GrantWriteData(lambdaFunction);

            // S3 Trigger for Lambda
            var imgExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            foreach (var ext in imgExtensions)
            {
                bucket.AddEventNotification(EventType.OBJECT_CREATED,
                    new LambdaDestination(lambdaFunction),
                    new NotificationKeyFilter
                    {
                        Prefix = "img/",
                        Suffix = ext
                    });
            }
        }
    }
}
