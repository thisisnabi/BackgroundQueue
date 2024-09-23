using BackgroundQueue.Core;
using FluentAssertions;

namespace BackgroundQueue.Tests
{
    public class BackgroundJobQueueTests
    {
        private readonly BackgroundJobQueue<IJob> _queue;
        private readonly int _capacity = 5;

        public BackgroundJobQueueTests()
        {
            _queue = new BackgroundJobQueue<IJob>(_capacity);
        }

        [Fact]
        public async Task QueueAsync_ShouldThrowArgumentNullException_WhenItemIsNull()
        {
            // Arrange
            Job1 jobItem = null!;

            // Act
            Func<Task> act = async () => await _queue.QueueAsync(jobItem);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>()
                .WithMessage("Value cannot be null. (Parameter 'Item')");
        }

        [Fact]
        public async Task QueueAsync_And_DequeueAsync_ShouldWorkCorrectly()
        {
            // Arrange
            Job1 jobItem = new Job1 { Id = 1 };
             
            // Act
            await _queue.QueueAsync(jobItem);
            var dequeuedItem = await _queue.DequeueAsync();

            // Assert
            dequeuedItem.Should().BeEquivalentTo(jobItem);
        }

        [Fact]
        public async Task QueueAsync_And_DequeueAsync_ShouldWorkInFifoOrder()
        {
            // Arrange
            Job1 firstItem = new Job1 { Id = 1 };
            Job1 secondItem = new Job1 { Id = 2 };

            // Act
            await _queue.QueueAsync(firstItem);
            await _queue.QueueAsync(secondItem);
        
            // Assert
            var dequeuedFirst = await _queue.DequeueAsync();
            var dequeuedSecond = await _queue.DequeueAsync();
        
            dequeuedFirst.Should().BeEquivalentTo(firstItem);
            dequeuedSecond.Should().BeEquivalentTo(secondItem);
        }

        [Fact]
        public async Task QueueAsync_And_DequeueAsync_ShouldWorkInFifoOrderDiffrentType()
        {
            // Arrange
            Job1 firstItem1 = new Job1 { Id = 1 };
            Job1 secondItem1 = new Job1 { Id = 2 };

            Job2 firstItem2 = new Job2 { Id = 1 };
            Job2 secondItem2 = new Job2 { Id = 2 };

            // Act
            await _queue.QueueAsync(firstItem1);
            await _queue.QueueAsync(firstItem2);
            await _queue.QueueAsync(secondItem1);
            await _queue.QueueAsync(secondItem2);

            // Assert
            var dequeuedFirst = await _queue.DequeueAsync();
            var dequeuedSecond = await _queue.DequeueAsync();

            dequeuedFirst.Should().BeEquivalentTo(firstItem);
            dequeuedSecond.Should().BeEquivalentTo(secondItem);
        }

        //[Fact]
        //public async Task DequeueAsync_ShouldHonorCancellationToken()
        //{
        //    // Arrange
        //    var cts = new CancellationTokenSource();
        //    cts.Cancel(); 

        //    // Act
        //    Func<Task> act = async () => await _queue.DequeueAsync(cts.Token);

        //    // Assert
        //    await act.Should().ThrowAsync<OperationCanceledException>();
        //}



        public class Job1 : IJob
        {
            public int Id { get; set; }
        }

        public class Job2 : IJob
        {
            public int Id { get; set; }

        }
    }
}