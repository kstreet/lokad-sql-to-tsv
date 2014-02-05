using NUnit.Framework;

namespace Lokad.SqlToTsv.Test
{
    [TestFixture]
    public class tsv_clean
    {
        [Test]
        public void already_clean()
        {
            const string input = "qwerty";
            Assert.AreEqual(input, Program.CleanTsv(input));
        }

        [Test]
        public void remove_tabs()
        {
            const string input = "a\tb\t c";
            Assert.AreEqual("a b  c", Program.CleanTsv(input));
        }

        [Test]
        public void remove_newlines()
        {
            const string input = "a \rt\n c";
            Assert.AreEqual("a  t  c", Program.CleanTsv(input));
        }

        [Test]
        public void remove_everything()
        {
            const string input = "a\r\t\nb";
            Assert.AreEqual("a   b", Program.CleanTsv(input));
        }
    }
}
