// 独立 NUnit 测试 harness（战斗技能系统）
// 目的:在 Unity Editor 已被占用、无法运行 Unity batchmode EditMode 测试时,
// 用纯 CLR 环境加载已编译的 Assembly-CSharp.dll 与 Assembly-CSharp-Editor.dll,
// 反射发现并运行 Mvp.EditorTests.Battle.Skills 命名空间下的 [Test] 方法。
// 完全反射驱动,编译期不引用任何 Unity / NUnit 程序集,通过 AssemblyResolve 探针目录解析运行时依赖。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace SkillTestsHarness
{
    internal static class Program
    {
        private static readonly List<string> ProbeDirs = new List<string>();

        private static int Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnResolve;

            // 可被解析的探针目录:harness 输出目录 + Unity 引擎托管目录 + 已编译程序集目录。
            ProbeDirs.Add(AppContext.BaseDirectory);
            ProbeDirs.Add(Path.Combine(AppContext.BaseDirectory, "unityengine"));
            ProbeDirs.Add(@"D:\unity\2022.3.62f3c1\Editor\Data\Managed");
            ProbeDirs.Add(@"D:\unity\2022.3.62f3c1\Editor\Data\Managed\UnityEngine");
            ProbeDirs.Add(@"D:\prounity\mvp\mvp\Temp\bin\Debug");
            ProbeDirs.Add(@"D:\prounity\mvp\mvp\Library\PackageCache\com.unity.ext.nunit@1.0.6\net35\unity-custom");

            var testAsmPath = args.Length > 0
                ? Path.GetFullPath(args[0])
                : @"D:\prounity\mvp\mvp\Temp\bin\Debug\Assembly-CSharp-Editor.dll";
            if (!File.Exists(testAsmPath))
            {
                Console.WriteLine("Test assembly not found: " + testAsmPath);
                return 2;
            }

            int pass = 0;
            int fail = 0;
            var failures = new List<string>();

            Assembly testAsm;
            try
            {
                testAsm = Assembly.LoadFrom(testAsmPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to load test assembly: " + ex);
                return 2;
            }

            Type[] types;
            try
            {
                types = testAsm.GetTypes()
                    .Where(t => t.Namespace != null
                        && t.Namespace.StartsWith("Mvp.EditorTests.Battle.Skills", StringComparison.Ordinal))
                    .ToArray();
            }
            catch (ReflectionTypeLoadException rtle)
            {
                Console.WriteLine("Type enumeration failed. Loader exceptions:");
                foreach (var e in rtle.LoaderExceptions)
                    Console.WriteLine("  " + e);
                return 2;
            }

            foreach (var type in types)
            {
                var testMethods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(m => m.GetCustomAttributes().Any(a => a.GetType().Name == "TestAttribute"))
                    .ToList();
                if (testMethods.Count == 0)
                    continue;

                if (type.IsAbstract)
                    continue;

                object fixture;
                try
                {
                    fixture = Activator.CreateInstance(type);
                }
                catch (MissingMethodException)
                {
                    // 无公开无参构造(如 MonoBehaviour 子类):分配实例但不运行构造函数。
                    fixture = FormatterServices.GetUninitializedObject(type);
                }

                var setUp = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(m => m.GetCustomAttributes().Any(a => a.GetType().Name == "SetUpAttribute"))
                    .ToArray();
                var tearDown = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(m => m.GetCustomAttributes().Any(a => a.GetType().Name == "TearDownAttribute"))
                    .ToArray();

                foreach (var m in testMethods)
                {
                    string name = type.Name + "." + m.Name;
                    try
                    {
                        foreach (var s in setUp)
                            s.Invoke(fixture, null);
                        try
                        {
                            m.Invoke(fixture, null);
                            pass++;
                            Console.WriteLine("PASS " + name);
                        }
                        finally
                        {
                            foreach (var t in tearDown)
                                t.Invoke(fixture, null);
                        }
                    }
                    catch (Exception ex)
                    {
                        fail++;
                        var inner = ex is TargetInvocationException tie ? (tie.InnerException ?? ex) : ex;
                        failures.Add(name + ": " + inner.GetType().Name + ": " + inner.Message);
                        Console.WriteLine("FAIL " + name + " :: " + inner.GetType().Name + ": " + inner.Message);
                        Console.WriteLine(inner.ToString());
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("==== " + pass + " passed, " + fail + " failed ====");
            foreach (var f in failures)
                Console.WriteLine("  [FAIL] " + f);
            return fail == 0 ? 0 : 1;
        }

        private static Assembly OnResolve(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name).Name;
            foreach (var dir in ProbeDirs)
            {
                var dll = Path.Combine(dir, name + ".dll");
                if (File.Exists(dll))
                {
                    try { return Assembly.LoadFrom(dll); }
                    catch { }
                }
                var exe = Path.Combine(dir, name + ".exe");
                if (File.Exists(exe))
                {
                    try { return Assembly.LoadFrom(exe); }
                    catch { }
                }
            }
            return null;
        }
    }
}
