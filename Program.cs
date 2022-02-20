using k8s;
using k8s.Models;
using System.Data;
using System.Text.Json;

var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(args[0]);
var client = new Kubernetes(config);

var ns = new V1Namespace
{
  Metadata = new V1ObjectMeta
  {
    Name = Guid.NewGuid().ToString()
  }
};

client.CreateNamespace(ns);

var pod = client.CreateNamespacedPod(new V1Pod
{
  Metadata = new V1ObjectMeta
  {
    Name = "mssqldb"
  },
  Spec = new V1PodSpec
  {
    Containers = new List<V1Container>
    {
      new V1Container
      {
        Name = "db",
        Image = "mcr.microsoft.com/mssql/server:2019-latest",
        Env = new List<V1EnvVar>
        {
          new V1EnvVar("ACCEPT_EULA", "Y"),
          new V1EnvVar("SA_PASSWORD", "Passw0rd!")
        },
        ReadinessProbe = new V1Probe
        {
          Exec = new V1ExecAction(new[] {"/opt/mssql-tools/bin/sqlcmd","-S","localhost","-U","sa","-P","Passw0rd!","-Q","'SELECT 1'" }),
          InitialDelaySeconds = 5,
          PeriodSeconds = 5,
          FailureThreshold = 5
        }
      }
    }
  }
}, ns.Metadata.Name);

do
{
  Task.Delay(500).Wait();
  pod = client.ReadNamespacedPodStatus(pod.Metadata.Name, pod.Metadata.NamespaceProperty);
} while (pod.Status.Phase == "Pending");

Console.WriteLine("MSSQL Running");

do
{
  Task.Delay(1000).Wait();
  pod = client.ReadNamespacedPodStatus(pod.Metadata.Name, pod.Metadata.NamespaceProperty);
} while (pod.Status.ContainerStatuses.Any(c => !c.Ready && c.RestartCount == 0));

if (pod.Status.ContainerStatuses.First().State.Waiting?.Reason == "CrashLoopBackoff" || pod.Status.ContainerStatuses.First().LastState.Terminated?.Reason == "Error")
{
  Console.WriteLine("Container crashed");
  using StreamReader reader = new StreamReader(client.ReadNamespacedPodLog(pod.Metadata.Name, pod.Metadata.NamespaceProperty, previous: true));
  var logs = reader.ReadToEnd();
  Console.WriteLine($"Logs: {logs}");
}


client.DeleteNamespace(ns.Metadata.Name);

Console.WriteLine("Done");