using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestHelper;
using CSharpImprovR;

namespace CSharpImprovR.Test
{
    [TestClass]
    public class UnitTest : CodeFixVerifier
    {

        // No diagnostics expected to show up
        [TestMethod]
        public void EmptySourceTest()
        {
            var test = @"";

            VerifyCSharpDiagnostic(test);
        }

        // Diagnostic and CodeFix both triggered and checked for
        [TestMethod]
        public void ThrowWithOneParameterTest()
        {
            var test = @"
    using System;

    namespace NS1
    {
        class T1
        {
            public void Foo(object paramName)   
            {
                if (paramName == null)
                {
                    throw new ArgumentNullException(""paramName"");
                }
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = NameOfAnalyzer.DiagnosticId,
                Message = String.Format(NameOfAnalyzer.MessageFormat, "paramName"),
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 12, 53)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"
    using System;

    namespace NS1
    {
        class T1
        {
            public void Foo(object paramName)   
            {
                if (paramName == null)
                {
                    throw new ArgumentNullException(nameof(paramName));
                }
            }
        }
    }";
            VerifyCSharpFix(test, fixtest);
        }

        // Diagnostic and CodeFix both triggered and checked for
        [TestMethod]
        public void ThrowWithTwoParametersTest()
        {
            var test = @"
    using System;

    namespace NS1
    {
        class T1
        {
            public void Foo(object paramName)   
            {
                if (paramName == null)
                {
                    throw new ArgumentNullException(""paramName"", ""paramName cannot be null!"");
                }
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = NameOfAnalyzer.DiagnosticId,
                Message = String.Format(NameOfAnalyzer.MessageFormat, "paramName"),
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 12, 53)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"
    using System;

    namespace NS1
    {
        class T1
        {
            public void Foo(object paramName)   
            {
                if (paramName == null)
                {
                    throw new ArgumentNullException(nameof(paramName), ""paramName cannot be null!"");
                }
            }
        }
    }";
            VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void NoIfNoDiagnosticTest()
        {
            var test = @"
    using System;

    namespace NS1
    {
        class T1
        {
            public void Foo(object paramName)   
            {
                if (paramName == paramName)
                {
                    throw new ArgumentNullException(""paramName"", ""paramName cannot be null!"");
                }
            }
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void NoIfWithNullCheckOfSameParameterNoDiagnosticTest()
        {
            var test = @"
    using System;

    namespace NS1
    {
        class T1
        {
            public void Foo(object paramName, object paramName2)   
            {
                if (paramName2 == null)
                {
                    throw new ArgumentNullException(""paramName"", ""paramName cannot be null!"");
                }
            }
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void NoIfWithNullCHeckNoDiagnosticTest()
        {
            var test = @"
    using System;

    namespace NS1
    {
        class T1
        {
            public void Foo(object paramName)   
            {
                throw new ArgumentNullException(""paramName"", ""paramName cannot be null!"");
            }
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new CSharpImprovRCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new NameOfAnalyzer();
        }
    }
}