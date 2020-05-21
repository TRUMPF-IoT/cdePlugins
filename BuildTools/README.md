# Build Tools

## Licensing

We use SHA1 for licensing purpose, where the bar is proving intention when somebody hacks it. We can’t use SHA256 because of the size of the digest: a user needs to be able to type in the activation key, so we are limiting ourselves it to 6 time 6 characters, which is about 170 bits.

 At some point we were generating two signatures (SHA1 / SHA256), but the additional complexity wasn’t worth the effort and risk at the time. We may revisit this in the future.

For Windows code signing, we use SHA1 because Windows XP can not verify SHA256 code signatures. SHA1 is still supported for code signing (<https://social.technet.microsoft.com/wiki/contents/articles/32288.windows-enforcement-of-sha1-certificates.aspx>).

**signtool.exe** is called from /buildtools/signandpackage.cmd and used for our signing.

