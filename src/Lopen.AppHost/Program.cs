var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Lopen>("lopen");

builder.Build().Run();
