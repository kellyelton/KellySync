# File Synchronization Between Two Locations

## Abstract
This document defines the requirements, process, and conditions of file synchronization. This document focuses on the logic and actions to be taken to synchronize files between two locations, and does not focus on any individual way of communicating the changes between the two locations. This is to say that where the files are in relation to eachother is irrelevent to this implementation. The operating system is also irrelevant to this document.

## Inconsequential 
- Where the locations are in relation to eachother

## Assumptions
- Both locations are on the same partition of the same hard drive
- Both locations have trustworty information. This doesn't take into account file integrety(except between the two locations)

## Requirements
### Conditions
	- Files last write time are different
	- Files sizes are different
	- Files hashes are different(this should be optional by file)
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
