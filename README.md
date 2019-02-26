# Loazit
Ivrit DRM removal

## Requirements
- Windows 10
- .NET Framework 4.5
- Visual Studio 2017

## Usage
0. Compile the project.
1. Download the official Ivrit app from the [Windows Store](https://www.microsoft.com/store/productId/9WZDNCRDN4QZ).
2. Launch the app, and click the button labeled "מזהה האפליקציה" on the login screen.
   A hex string will appear.
   Copy it to a safe place.
3. Log into the app, and download a book.
4. Locate the book files on disk. They should be in
   `C:\Users\<username>\AppData\Local\Packages\<application identifier>\LocalState\com_yit_evrit<book id>`.
   Look for files with the `.npkg` and `.enc` extensions.
5. Run the decryptor: `Loazit.exe bookFile outputFile [appId [bookKeyFile]]`
   - `bookFile` is the path to the `.npkg` file.
   - `outputFile` is the path for the decrypted `.epub`.
   - `appId` is the hex string we copied from the login screen.
     This should be omitted if the book is not encrypted (there is no `.enc` file).
   - `bookKeyFile` is the `.enc` file in the same directory as the `.npkg` file.
     This can be omitted if both files have the same name.
