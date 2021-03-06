using KellySync;
// <copyright file="FilePathFactory.cs">Copyright ©  2016</copyright>

using System;
using Microsoft.Pex.Framework;

namespace KellySync
{
    /// <summary>A factory for KellySync.FilePath instances</summary>
    public static partial class FilePathFactory
    {
        /// <summary>A factory for KellySync.FilePath instances</summary>
        [PexFactoryMethod( typeof( FilePath ) )]
        public static FilePath Create( string path_s, Config config_config ) {
            FilePath filePath = new FilePath( path_s, config_config );
            return filePath;
            // TODO: Edit factory method of FilePath
            // This method should be able to configure the object in all possible ways.
            // Add as many parameters as needed,
            // and assign their values to each field by using the API.
        }
    }

    public static partial class ConfigFactory
    {
        /// <summary>A factory for KellySync.Config instances</summary>
        [PexFactoryMethod( typeof( Config ) )]
        public static Config Create( ) {
            Config config = new Config( );
            return config;
        }
    }
}
