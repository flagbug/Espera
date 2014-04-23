using System;
using Xunit;

namespace Espera.Core.Tests
{
    public class TagSanitizerTest
    {
        public class TheSanitizeMethod
        {
            [Fact]
            public void ThrowsArgumentNullExceptionIfTagIsNull()
            {
                Assert.Throws<ArgumentNullException>(() => TagSanitizer.Sanitize(null));
            }

            [Fact]
            public void WithInvalidXmlCharacter()
            {
                const string tag = "A\u0018B";

                string sanitized = TagSanitizer.Sanitize(tag);

                Assert.Equal("A_B", sanitized);
            }
        }
    }
}