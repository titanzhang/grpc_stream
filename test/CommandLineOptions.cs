using CommandLine;

namespace test
{
    interface IOptions
    {
        [Option('m', "mode",
            Required = true,
            HelpText = "Access mode.")]
        AccessMode Mode { get; set; }

        [Option('k', "key",
            Required = true,
            HelpText = "File path/key.")]
        string Key { get; set; }

        [Option('r', "repeats",
            Default = 10,
            Required = false,
            HelpText = "How many times the operation will be performed")]
        int RepeatCount { get; set; }
    }

    public enum AccessMode
    {
        disk, gridfs, grpc
    }

    [Verb("write", false, HelpText = "Test writing operation.")]
    class WriteOptions: IOptions
    {
        public AccessMode Mode { get; set; }
        public string Key { get; set; }
        public int RepeatCount { get; set; }
        [Option('s', "size",
            Required = true,
            HelpText = "File size (byte)")]
        public int FileSize { get; set; }
    }

    [Verb("read", false, HelpText = "Test reading operation.")]
    class ReadOptions: IOptions
    {
        public AccessMode Mode { get; set; }
        public string Key { get; set; }
        public int RepeatCount { get; set; }
    }
}