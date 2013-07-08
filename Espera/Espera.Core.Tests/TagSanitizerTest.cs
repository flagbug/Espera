using System;
using Xunit;

namespace Espera.Core.Tests
{
    public class TagSanitizerTest
    {
        [Fact]
        public void SanitizedInvalidXmlCharacter()
        {
            const string tag = "A\u0018B";

            string sanitized = TagSanitizer.Sanitize(tag);

            Assert.Equal("A_B", sanitized);
        }

        [Fact]
        public void SanitizeThrowsArgumentNullExceptionIfTagIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => TagSanitizer.Sanitize(null));
        }
    }
}