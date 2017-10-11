using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using RSG.Promises;

namespace Playground1
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("---- start ----");
            Promise p1 = new Promise();
            Thread t = new Thread(() =>
            {
                Console.WriteLine("p1 start");
                Thread.Sleep(200);
                Console.WriteLine("P1 end");
                p1.Resolve();
            });   
            p1.AddOnCancel(() =>
            {
                Console.WriteLine("p1 canceled and thread terminated");
                t.Interrupt();
            });
            
            
            Promise p2 = new Promise((resolve, reject) =>
            {
                Thread t2 = new Thread(() =>
                {
                    Console.WriteLine("p2 start");
                    Thread.Sleep(100);
                    Console.WriteLine("P2 end");
                    resolve();
                });
                t2.Start();
            }, () => Console.WriteLine("p2 canceled"));
                        
            Console.WriteLine("startRace ???");
            Promise.Race(p1,p2).Done(() => Console.WriteLine("----done----"));
            
            Console.WriteLine("----end --- ");
        }
    }
}