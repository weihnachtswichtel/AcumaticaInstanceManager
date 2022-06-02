# AcumaticaInstanceManager

This is a demo project that suggests a way to automate the deployment and management of Acumatica ERP instances.
It is a implementation of a wrapper that interacts with build.acumatica.com S3 storage, downloading and handling MSI packages, configuring ac.exe and more.
The main purpose of this project is to provide some insights and initial codebase for Acumatica developers,
who would like to incorporate the instance management process into their CI/CD.
This, however can be used as a stand-alone tool that will speedify the Acumatica ERP deployment process.


## Available Commands

### List builds of Acumatica ERP

    --listbuilds [Major Acumatica ERP version]

Major Acumatica ERP version is and optional parameter. If could be specified as 21.2, 21R2 or 2021R2
If not specified, all available builds for the versions after 20 Rx will be listed.

### Install Acumatica ERP

    --install [Major Acumatica ERP Version] [Acumatica ERP build]

Both, Acumatica version and build are optional but only one should be used. If no optional parameters specified, the latest available build for the latest major release will be taken.
Optional Major Acumatica ERP version parameter could be specified as 21.2, 21R2 or 2021R2. If specified the latest build of this version will be taken. Beta builds would be ignored.
If Acumatica ERP build parameter specified, the specified build will be taked for deployment

### Remove Acumatica ERP

	--remove <InstanceName> 

This command removes the Acumatica ERP instance that is specified as the required parameter.

**Important**: Only use it for those sites that were deployed by this tool or after review/change of the settings and code.

The command will remove based on the InstanceName **socalled** following items:

- Site Files
- Database (if set)
- IIS Application
- IIS Pool
- RegistryRecords
- Temp Files
- Customizations Files
- Snapshots
	
	
	