package com.lux.ticker.backup

import android.app.backup.BackupAgent
import android.app.backup.BackupDataInput
import android.app.backup.BackupDataOutput
import android.app.backup.FullBackupDataOutput
import android.os.ParcelFileDescriptor
import java.io.File
import java.io.FileInputStream

/**
 * Auto Backup restores the previous snapshot when backup rules change.
 * That restore was replacing the live DataStore on update. If the file is
 * already on disk, keep it and discard the backed-up copy.
 */
class LuxBackupAgent : BackupAgent() {
    override fun onBackup(oldState: ParcelFileDescriptor?, data: BackupDataOutput?, newState: ParcelFileDescriptor?) = Unit

    override fun onRestore(data: BackupDataInput?, appVersionCode: Int, newState: ParcelFileDescriptor?) = Unit

    override fun onFullBackup(data: FullBackupDataOutput) {
        super.onFullBackup(data)
    }

    override fun onRestoreFile(
        data: ParcelFileDescriptor,
        size: Long,
        destination: File,
        type: Int,
        mode: Long,
        mtime: Long,
    ) {
        val existing = if (destination.exists()) destination.length() else 0L
        if (BackupPolicy.keepLocalFile(destination.path, existing)) {
            val input = FileInputStream(data.fileDescriptor)
            val buffer = ByteArray(8192)
            var left = size
            while (left > 0L) {
                val read = input.read(buffer, 0, minOf(buffer.size.toLong(), left).toInt())
                if (read <= 0) break
                left -= read
            }
            return
        }
        super.onRestoreFile(data, size, destination, type, mode, mtime)
    }
}

object BackupPolicy {
    fun keepLocalFile(destinationPath: String, existingLength: Long): Boolean {
        if (existingLength <= 0L) return false
        val path = destinationPath.replace('\\', '/')
        return path.contains("/datastore/")
    }
}
