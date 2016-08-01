# Syncing

## Requirements
### Conditions
	- Files last write time are different
	- Files sizes are different
	- Files hashes are different
	- Files names are different

### Restrictions
- Must not have had an event in the past 5 seconds
- Neither file can have the following attributes
	- Offline
	- ReadOnly

## Process
### For Folders
- Both folders will be created if they don't already exist.
- Both files will be created if neccisary and then opened with FileShare.None. If this fails, the sync can't continue.
