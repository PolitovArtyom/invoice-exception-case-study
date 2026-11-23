using InvoiceDesk.Adapter;

Console.InputEncoding = System.Text.Encoding.UTF8;
Console.OutputEncoding = System.Text.Encoding.UTF8;

return Adapter.Run(args, Console.In, Console.Out);
