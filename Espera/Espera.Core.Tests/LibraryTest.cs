using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Espera.Core.Tests
{
    [TestClass]
    public class LibraryTest
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CreateAdmin_PasswordIsNull_ThrowsArgumentNullException()
        {
            var library = new Library.Library();
            library.CreateAdmin(null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void CreateAdmin_PasswordIsEmpty_ThrowsArgumentException()
        {
            var library = new Library.Library();
            library.CreateAdmin(String.Empty);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void CreateAdmin_PasswordIsWhitespace_ThrowsArgumentException()
        {
            var library = new Library.Library();
            library.CreateAdmin(" ");
        }

        [TestMethod]
        public void CreateAdmin_PasswordIsTestPassword_AdministratorIsCreated()
        {
            var library = new Library.Library();
            library.CreateAdmin("TestPassword");

            Assert.IsTrue(library.IsAdministratorCreated);
        }
    }
}