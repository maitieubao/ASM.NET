using System;
using BCrypt.Net;

string password = "Admin@123";
string hash = BCrypt.Net.BCrypt.HashPassword(password);
Console.WriteLine(hash);
