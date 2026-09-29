using System.Threading;
using WireDrop;
using Xunit;

namespace WireDrop.Tests
{
    public class DeadlineTests
    {
        [Fact]
        public void Not_passed_as_soon_as_it_is_set()
        {
            Assert.False(Deadline.After().Passed);
        }

        [Fact]
        public void Passed_once_its_time_is_up()
        {
            var deadline = Deadline.After(20);
            Thread.Sleep(40);
            Assert.True(deadline.Passed);
        }

        [Fact]
        public void A_zero_deadline_has_already_passed()
        {
            Assert.True(Deadline.After(0).Passed);
        }

        [Fact]
        public void Three_seconds_by_default()
        {
            Assert.Equal(3000, Deadline.LimitMs);
        }
    }
}
