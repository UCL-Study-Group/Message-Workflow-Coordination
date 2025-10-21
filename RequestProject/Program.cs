namespace RequestProject
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var coordinator = new WorkflowCoordinator(
                "Host=localhost;Port=5432;Database=workflowdb;Username=postgres;Password=mysecretpassword;Enlist=true",
                "localhost"
            );

            await coordinator.StartWorkAsync(workItemID: 1);
            await coordinator.StartWorkAsync(workItemID: 2);
            await coordinator.StartWorkAsync(workItemID: 3);

            await coordinator.CompleteWorkAsync();

            Console.WriteLine("Coordinator is running. Press any key to exit...");
            Console.ReadKey();
        }
    }
}
