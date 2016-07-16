// config.json
// Lives with the exe file
{
    "profiles": {
        "Home": "ENCRYPTED_STRING",
        "Work": "ENCRYPTED_STRING", 
        "Shared": "ENCRYPTED_STRING"
    }
}

// These are the data encrypted in the config.json profiles elements 
{
    // Home.profileconfig
    // Lives with the exe file
    {
        "name": "Home",
        "paths":[
            "~\.ssh\*",
            "~\.bashrc"
        ]
    }

    // Shared.profileconfig
    // Lives with the exe file
    {
        "name": "Shared",
        "paths":[
            "~\.vimrc",
            "~\.gitconfig",
            "~\.vimfiles\*"
        ]
    }

    // Work.profileconfig
    {
        "name": "Work",
        "paths":[
            "~\.ssh\*",
            "~\.bashrc"
        ]
    }
}

// machineconfig.json
// Lives in app data
{
    "profiles":  {
        "Home": { 
            "name": "Home",
            "paths":[
                "~\.ssh\*",
                "~\.bashrc"
            ]
        },
        "Shared": {
            "name": "Shared",
            "paths":[
                "~\.vimrc",
                "~\.gitconfig",
                "~\.vimfiles\*"
            ]
        }
    }
}

// Folder Structure on computer using 'Home' and 'Shared' profiles
// 
// %LOCALAPPDATA%\machineconfig.json        \\local config file with unencrypted profiles 
// \program.exe                             \\Main Program
// \config.json                             \\config file
// 
// 
// 
// 
// 
// 
// 
// 

