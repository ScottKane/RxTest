using RxTest;

var events = new EventBus();

events.Subscribe((ref TestEvent e) =>
{
    Console.WriteLine("A");
    Console.WriteLine($"A Handled: {e.Handled}");
    e.Handled = true;
    Console.WriteLine($"A Handled: {e.Handled}");
});
events.Subscribe((ref TestEvent e) =>
{
    Console.WriteLine("B");
    Console.WriteLine($"B Handled: {e.Handled}");
});

events.Publish(new TestEvent());
Console.ReadLine();