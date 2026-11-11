// 마중불 검증용 최소 NUnit 대체 + 실행기 (tools/verify/verify.sh 전용)
//
// Assets/Tests/EditMode/CoreTests.cs 를 Unity 없이 mono 로 돌리기 위해 NUnit 중 그 파일이 쓰는 것만 흉내 낸다:
//   [Test], Assert.AreEqual / IsTrue / IsFalse
// 테스트가 이 밖의 기능([TestCase], [SetUp], Assert.That, Assert.Throws 등)을 쓰기 시작하면 mcs 컴파일이
// 실패한다(조용히 건너뛰지 않는다). 그때는 여기에 같은 의미로 추가하거나, 진짜 NUnit으로 돌린다.
//
// 실행기는 [Test] 메서드를 모두 찾아 실행하고, 실패가 있거나 하나도 찾지 못하면 0이 아닌 값으로 끝난다.
// 첫 번째 인자로 기대 테스트 수를 주면(verify.sh 가 소스의 [Test] 개수를 센다) 찾은 수와 다를 때도 실패한다.
using System;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Method)] public class TestAttribute : Attribute { }

    public static class Assert
    {
        // NUnit 3 과 같은 순서: (expected, actual). 숫자는 NUnit처럼 형식이 달라도 값으로 비교한다.
        public static void AreEqual(object expected, object actual)
        {
            if (Same(expected, actual)) return;
            throw new AssertionException("기대값 <" + Show(expected) + "> 실제값 <" + Show(actual) + ">");
        }
        public static void IsTrue(bool condition) { if (!condition) throw new AssertionException("참이어야 하는데 거짓"); }
        public static void IsFalse(bool condition) { if (condition) throw new AssertionException("거짓이어야 하는데 참"); }

        static bool Same(object a, object b)
        {
            if (Equals(a, b)) return true;
            if (IsNumeric(a) && IsNumeric(b)) return Convert.ToDecimal(a) == Convert.ToDecimal(b);
            return false;
        }
        static bool IsNumeric(object o) =>
            o is int || o is long || o is short || o is byte || o is uint || o is ulong || o is ushort || o is sbyte || o is decimal;
        static string Show(object o) => o == null ? "null" : o.ToString();
    }

    public class AssertionException : Exception { public AssertionException(string m) : base(m) { } }
}

public static class ShimRunner
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        int expected = args.Length > 0 ? int.Parse(args[0]) : -1;
        int pass = 0, fail = 0;
        var asm = typeof(ShimRunner).Assembly;
        foreach (var t in asm.GetTypes().OrderBy(x => x.FullName, StringComparer.Ordinal))
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (m.GetCustomAttributes(typeof(NUnit.Framework.TestAttribute), false).Length == 0) continue;
                try
                {
                    object self = m.IsStatic ? null : Activator.CreateInstance(t);
                    m.Invoke(self, null);
                    pass++;
                    Console.WriteLine("  통과 " + t.Name + "." + m.Name);
                }
                catch (Exception e)
                {
                    fail++;
                    var inner = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
                    Console.WriteLine("  실패 " + t.Name + "." + m.Name + ": " + inner.GetType().Name + ": " + inner.Message);
                }
            }
        Console.WriteLine("  테스트 " + (pass + fail) + "개 중 통과 " + pass + ", 실패 " + fail);
        if (pass + fail == 0) { Console.WriteLine("  [Test] 메서드를 하나도 찾지 못했다."); return 1; }
        if (expected >= 0 && expected != pass + fail)
        {
            Console.WriteLine("  소스의 [Test] 수(" + expected + ")와 실행한 수(" + (pass + fail) + ")가 다르다.");
            return 1;
        }
        return fail == 0 ? 0 : 1;
    }
}
