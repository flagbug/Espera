using NUnit.Framework;
using System;

namespace Espera.Core.Tests
{
    [TestFixture]
    public class TagSanitizerTest
    {
        [Test]
        public void Sanitize_TagContainsInvalidXmlCharacter_ReturnsSanitizedString()
        {
            const string tag = "A\u0018B";

            string sanitized = TagSanitizer.Sanitize(tag);

            Assert.AreEqual("A_B", sanitized);
        }

        [Test]
        public void Sanitize_TagIsNull_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => TagSanitizer.Sanitize(null));
        }
    }
}