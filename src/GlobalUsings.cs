﻿global using ACR_SyncTool.DockerClient;
global using ACR_SyncTool.DockerClient.Authentication;
global using ACR_SyncTool.DockerClient.Extensionis;
global using ACR_SyncTool.DockerClient.OAuth;
global using ACR_SyncTool.Models;
global using Azure.Containers.ContainerRegistry;
global using Azure.Identity;
global using Docker.DotNet;
global using Docker.DotNet.Models;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.Configuration.Json;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;
global using Polly;
global using System;
global using System.Collections.Generic;
global using System.Diagnostics;
global using System.IO;
global using System.Linq;
global using System.Net;
global using System.Net.Http;
global using System.Net.Http.Headers;
global using System.Net.Http.Json;
global using System.Security.Authentication;
global using System.Security.Cryptography.X509Certificates;
global using System.Text;
global using System.Text.Json;
global using System.Text.Json.Serialization;
global using System.Text.RegularExpressions;
global using System.Threading;
global using System.Threading.Tasks;
