// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");
Console.WriteLine(BCrypt.Net.BCrypt.HashPassword("admin123"));
Console.WriteLine(BCrypt.Net.BCrypt.HashPassword("test123"));
Console.ReadKey();