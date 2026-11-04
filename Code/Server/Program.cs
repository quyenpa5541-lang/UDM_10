using System;
using System.Net;
using Microsoft.Extensions.Configuration;

class Program
{
    static void Main(string[] args)
    { 
        
        var config = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        

        string host = config["NetworkSettings:Host"] ?? "127.0.0.1";
        int port = config.GetValue<int>("NetworkSettings:Port", 9000);
        string uploaddir = config["NetworkSettings:UploadDir"] ?? "uploads";
        long maxfilesizemb = config.GetValue<long>("NetworkSettings:MaxFileSizeMB", 500)*1024*1024;
        int maxconnection = config.GetValue<int>("NetworkSettings:MaxConnections", 100);
        int paralleluploads = config.GetValue<int>("NetworkSettings:ParallelUploads", 3);
        int timeoutseconds = config.GetValue<int>("NetworkSettings:TimeoutSeconds", 30);
        

        
        IPAddress ipAddress = IPAddress.Parse(host);
        IPEndPoint remoteEndPoint = new IPEndPoint(ipAddress,port);
        int timeoutMilliseconds = timeoutseconds * 1000;
        
        string absoluteUploadDir = Path.GetFullPath(uploaddir);
    }
}