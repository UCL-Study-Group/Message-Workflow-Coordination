namespace ReplyProject
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var worker = new WorkItemProcessor();

            await worker.StartProcessingAsync("localhost");

            Console.WriteLine("Worker is running. Press any key to exit...");
            Console.ReadKey();
        }
    }
}
