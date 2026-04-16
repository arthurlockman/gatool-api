using Amazon.CDK;
using Amazon.CDK.AWS.CertificateManager;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ECS.Patterns;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SSM;
using Constructs;
using EnableScalingProps = Amazon.CDK.AWS.ApplicationAutoScaling.EnableScalingProps;
using HealthCheck = Amazon.CDK.AWS.ElasticLoadBalancingV2.HealthCheck;
using Schedule = Amazon.CDK.AWS.Events.Schedule;
using CronOptions = Amazon.CDK.AWS.Events.CronOptions;
using Attribute = Amazon.CDK.AWS.DynamoDB.Attribute;

namespace InfraCdk;

public class GatoolStack : Stack
{
    public GatoolStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
    {
        // ── VPC ──────────────────────────────────────────────────────────
        var vpc = new Vpc(this, "GatoolVpc", new VpcProps
        {
            MaxAzs = 2,
            NatGateways = 0,
            VpcName = "gatool-vpc",
            SubnetConfiguration =
            [
                new SubnetConfiguration
                {
                    Name = "Public",
                    SubnetType = SubnetType.PUBLIC,
                    CidrMask = 24
                }
            ]
        });

        // ── S3 Buckets (import existing) ────────────────────────────────
        var bucketNames = new[] { "gatool-high-scores", "gatool-team-updates", "gatool-team-updates-history", "gatool-user-preferences" };
        var buckets = new List<IBucket>();
        foreach (var name in bucketNames)
        {
            var bucket = Bucket.FromBucketName(this, name, name);
            buckets.Add(bucket);
        }

        // ── DynamoDB Tables ───────────────────────────────────────────────
        var highScoresTable = new Table(this, "HighScoresTable", new TableProps
        {
            TableName = "gatool-high-scores",
            PartitionKey = new Attribute { Name = "Year", Type = AttributeType.NUMBER },
            SortKey = new Attribute { Name = "ScoreKey", Type = AttributeType.STRING },
            BillingMode = BillingMode.PAY_PER_REQUEST,
            RemovalPolicy = RemovalPolicy.RETAIN
        });

        // ── ECS Cluster ─────────────────────────────────────────────────
        var cluster = new Cluster(this, "GatoolCluster", new ClusterProps
        {
            Vpc = vpc,
            ClusterName = "gatool",
            EnableFargateCapacityProviders = true
        });

        // Read the deployed image tag from SSM (written by CI/CD pipeline)
        var imageTag = StringParameter.ValueForStringParameter(this, "/gatool/image-tag");
        var appImage = ContainerImage.FromRegistry(
            Fn.Join("", new[] { "ghcr.io/arthurlockman/gatool-api:", imageTag }));

        // ── Task Definition (API + Redis sidecar) ───────────────────────
        var taskDef = new FargateTaskDefinition(this, "GatoolApiTask", new FargateTaskDefinitionProps
        {
            Cpu = 1024,         // 1 vCPU
            MemoryLimitMiB = 2048, // 2 GB (shared with Redis sidecar)
            RuntimePlatform = new RuntimePlatform
            {
                CpuArchitecture = CpuArchitecture.ARM64,
                OperatingSystemFamily = OperatingSystemFamily.LINUX
            }
        });

        // API container
        var apiContainer = taskDef.AddContainer("gatool-api", new ContainerDefinitionOptions
        {
            Image = appImage,
            Logging = LogDriver.AwsLogs(new AwsLogDriverProps
            {
                StreamPrefix = "gatool-api",
                LogRetention = RetentionDays.ONE_MONTH
            }),
            PortMappings = [new PortMapping { ContainerPort = 8080 }],
            HealthCheck = new Amazon.CDK.AWS.ECS.HealthCheck
            {
                Command = ["CMD-SHELL", "curl -f http://localhost:8080/livecheck || exit 1"],
                Interval = Duration.Seconds(30),
                Timeout = Duration.Seconds(5),
                StartPeriod = Duration.Seconds(10),
                Retries = 3
            },
            Environment = new Dictionary<string, string>
            {
                ["Redis__Host"] = "localhost",
                ["Redis__Port"] = "6379",
                ["Redis__UseTls"] = "false",
                ["Redis__Password"] = "",
                ["NEW_RELIC_APP_NAME"] = "gatool-api",
                ["NODE_ENV"] = "production"
            },
            Secrets = new Dictionary<string, Secret>
            {
                ["NEW_RELIC_LICENSE_KEY"] = Secret.FromSecretsManager(
                    Amazon.CDK.AWS.SecretsManager.Secret.FromSecretNameV2(this, "NewRelicSecret", "NewRelicLicenseKey"))
            }
        });

        // Redis sidecar
        taskDef.AddContainer("redis", new ContainerDefinitionOptions
        {
            Image = ContainerImage.FromRegistry("redis:7-alpine"),
            Logging = LogDriver.AwsLogs(new AwsLogDriverProps
            {
                StreamPrefix = "redis",
                LogRetention = RetentionDays.ONE_WEEK
            }),
            Command = ["redis-server", "--maxmemory", "256mb", "--maxmemory-policy", "allkeys-lru"],
            Essential = false,
            HealthCheck = new Amazon.CDK.AWS.ECS.HealthCheck
            {
                Command = ["CMD", "redis-cli", "ping"],
                Interval = Duration.Seconds(30),
                Timeout = Duration.Seconds(5),
                Retries = 3,
                StartPeriod = Duration.Seconds(10)
            }
        });

        // Grant S3 access to the task role
        foreach (var bucket in buckets)
            bucket.GrantReadWrite(taskDef.TaskRole);

        // Grant DynamoDB access
        highScoresTable.GrantReadWriteData(taskDef.TaskRole);

        // Grant Secrets Manager access
        taskDef.TaskRole.AddManagedPolicy(
            ManagedPolicy.FromAwsManagedPolicyName("SecretsManagerReadWrite"));

        // ── ACM Certificate ─────────────────────────────────────────────
        // Use pre-created wildcard certificate for *.gatool.org
        var certificate = Certificate.FromCertificateArn(this, "ApiCert",
            "arn:aws:acm:us-east-2:069176179806:certificate/c1811a5e-c07b-4804-ab6d-9bdd033beb6d");

        // ── Fargate Service + ALB ───────────────────────────────────────
        var fargateService = new ApplicationLoadBalancedFargateService(
            this, "GatoolApiService", new ApplicationLoadBalancedFargateServiceProps
            {
                Cluster = cluster,
                TaskDefinition = taskDef,
                ServiceName = "gatool-api",
                DesiredCount = 2,
                PublicLoadBalancer = true,
                ListenerPort = 443,
                Certificate = certificate,
                RedirectHTTP = true,
                TaskSubnets = new SubnetSelection { SubnetType = SubnetType.PUBLIC },
                AssignPublicIp = true,
                HealthCheckGracePeriod = Duration.Seconds(300),
                CapacityProviderStrategies =
                [
                    new CapacityProviderStrategy { CapacityProvider = "FARGATE_SPOT", Weight = 4 },
                    new CapacityProviderStrategy { CapacityProvider = "FARGATE", Weight = 1, Base = 1 }
                ]
            });

        // Configure health check on ALB target group
        fargateService.TargetGroup.ConfigureHealthCheck(new HealthCheck
        {
            Path = "/livecheck",
            HealthyHttpCodes = "200",
            Interval = Duration.Seconds(30),
            Timeout = Duration.Seconds(10),
            HealthyThresholdCount = 2,
            UnhealthyThresholdCount = 3
        });

        // Auto-scaling: 1 to 5 based on request count
        var scaling = fargateService.Service.AutoScaleTaskCount(new EnableScalingProps
        {
            MinCapacity = 2,
            MaxCapacity = 8
        });
        scaling.ScaleOnRequestCount("RequestScaling", new RequestCountScalingProps
        {
            TargetGroup = fargateService.TargetGroup,
            RequestsPerTarget = 1000
        });

        // ── Scheduled Tasks ─────────────────────────────────────────────
        // Shared task definition for jobs (smaller resources)
        var jobTaskDef = new FargateTaskDefinition(this, "GatoolJobTask", new FargateTaskDefinitionProps
        {
            Cpu = 256,         // 0.25 vCPU
            MemoryLimitMiB = 512,  // 0.5 GB
            RuntimePlatform = new RuntimePlatform
            {
                CpuArchitecture = CpuArchitecture.ARM64,
                OperatingSystemFamily = OperatingSystemFamily.LINUX
            }
        });

        jobTaskDef.AddContainer("job", new ContainerDefinitionOptions
        {
            Image = appImage,
            Logging = LogDriver.AwsLogs(new AwsLogDriverProps
            {
                StreamPrefix = "gatool-jobs",
                LogRetention = RetentionDays.ONE_MONTH
            }),
            Environment = new Dictionary<string, string>
            {
                ["Redis__Host"] = "localhost",
                ["Redis__Port"] = "6379",
                ["Redis__UseTls"] = "false",
                ["Redis__Password"] = "",
                ["NEW_RELIC_APP_NAME"] = "gatool-api",
                ["NODE_ENV"] = "production"
            },
            Secrets = new Dictionary<string, Secret>
            {
                ["NEW_RELIC_LICENSE_KEY"] = Secret.FromSecretsManager(
                    Amazon.CDK.AWS.SecretsManager.Secret.FromSecretNameV2(this, "NewRelicSecretJob", "NewRelicLicenseKey"))
            }
        });

        // Redis sidecar for jobs
        jobTaskDef.AddContainer("redis", new ContainerDefinitionOptions
        {
            Image = ContainerImage.FromRegistry("redis:7-alpine"),
            Command = ["redis-server", "--maxmemory", "128mb", "--maxmemory-policy", "allkeys-lru"],
            Essential = false
        });

        // Grant same access to job task role
        foreach (var bucket in buckets)
            bucket.GrantReadWrite(jobTaskDef.TaskRole);
        highScoresTable.GrantReadWriteData(jobTaskDef.TaskRole);
        jobTaskDef.TaskRole.AddManagedPolicy(
            ManagedPolicy.FromAwsManagedPolicyName("SecretsManagerReadWrite"));

        // UpdateGlobalHighScores - every 15 minutes
        new Rule(this, "HighScoresSchedule", new RuleProps
        {
            RuleName = "gatool-update-high-scores",
            Schedule = Schedule.Cron(new CronOptions { Minute = "*/15" }),
            Description = "Update global high scores every 15 minutes"
        }).AddTarget(new EcsTask(new EcsTaskProps
        {
            Cluster = cluster,
            TaskDefinition = jobTaskDef,
            SubnetSelection = new SubnetSelection { SubnetType = SubnetType.PUBLIC },
            AssignPublicIp = true,
            ContainerOverrides =
            [
                new ContainerOverride
                {
                    ContainerName = "job",
                    Command = ["--job", "UpdateGlobalHighScores"]
                }
            ]
        }));

        // ── Outputs ─────────────────────────────────────────────────────
        new CfnOutput(this, "AlbDnsName", new CfnOutputProps
        {
            Value = fargateService.LoadBalancer.LoadBalancerDnsName,
            Description = "ALB DNS name - point api.gatool.org CNAME here"
        });

        new CfnOutput(this, "ClusterName", new CfnOutputProps
        {
            Value = cluster.ClusterName,
            Description = "ECS cluster name"
        });
    }
}
