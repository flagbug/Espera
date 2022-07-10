using System;
using Xunit;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace Espera.Core.Tests
{
    public class TagSanitizerTest
    {
        public class TheSanitizeMethod
        {
            [Fact]
            public void ThrowsArgumentNullExceptionIfTagIsNull()
            {
                Xunit.Assert.Throws<ArgumentNullException>(() => TagSanitizer.Sanitize(null));
            }

            [Fact]
            public void WithInvalidXmlCharacter()
            {
                const string tag = "A\u0018B";

                var sanitized = TagSanitizer.Sanitize(tag);

                Assert.AreEqual("A_B", sanitized);
            }
        }
    }
}