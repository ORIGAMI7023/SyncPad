/**
 * 端到端加密 JavaScript 库
 * 使用 Web Crypto API 实现 PBKDF2 密钥派生和 AES-GCM 加密
 */

class E2EECrypto {
    constructor() {
        this.key = null; // 派生的主密钥 (CryptoKey)
        this.dbName = 'SyncPadE2EE';
        this.dbVersion = 1;
        this.keyStoreName = 'encryptionKeys';
    }

    /**
     * 从密码和盐值派生主密钥
     * @param {string} password - 用户密码
     * @param {string} saltBase64 - Base64编码的盐值
     * @returns {Promise<CryptoKey>} 派生的密钥
     */
    async deriveKey(password, saltBase64) {
        const encoder = new TextEncoder();
        const passwordBuffer = encoder.encode(password);
        const saltBuffer = this.base64ToArrayBuffer(saltBase64);

        // 导入密码作为密钥材料
        const passwordKey = await crypto.subtle.importKey(
            'raw',
            passwordBuffer,
            'PBKDF2',
            false,
            ['deriveKey']
        );

        // 使用 PBKDF2 派生 AES-GCM 密钥
        const key = await crypto.subtle.deriveKey(
            {
                name: 'PBKDF2',
                salt: saltBuffer,
                iterations: 100000,
                hash: 'SHA-256'
            },
            passwordKey,
            { name: 'AES-GCM', length: 256 },
            false, // 不可导出，提高安全性
            ['encrypt', 'decrypt']
        );

        this.key = key;
        return key;
    }

    /**
     * 加密文本
     * @param {string} plaintext - 明文
     * @returns {Promise<{encryptedData: string, iv: string}>} 加密结果（Base64编码）
     */
    async encrypt(plaintext) {
        if (!this.key) {
            throw new Error('密钥未初始化，请先调用 deriveKey');
        }

        const encoder = new TextEncoder();
        const dataBuffer = encoder.encode(plaintext);

        // 生成随机IV（12字节，AES-GCM推荐）
        const iv = crypto.getRandomValues(new Uint8Array(12));

        // 加密数据
        const encryptedBuffer = await crypto.subtle.encrypt(
            { name: 'AES-GCM', iv: iv },
            this.key,
            dataBuffer
        );

        // 转换为Base64
        const encryptedData = this.arrayBufferToBase64(encryptedBuffer);
        const ivBase64 = this.arrayBufferToBase64(iv);

        return { encryptedData, iv: ivBase64 };
    }

    /**
     * 解密文本
     * @param {string} encryptedDataBase64 - Base64编码的加密数据
     * @param {string} ivBase64 - Base64编码的IV
     * @returns {Promise<string>} 解密后的明文
     */
    async decrypt(encryptedDataBase64, ivBase64) {
        if (!this.key) {
            throw new Error('密钥未初始化，请先调用 deriveKey');
        }

        const encryptedBuffer = this.base64ToArrayBuffer(encryptedDataBase64);
        const iv = this.base64ToArrayBuffer(ivBase64);

        // 解密数据
        const decryptedBuffer = await crypto.subtle.decrypt(
            { name: 'AES-GCM', iv: iv },
            this.key,
            encryptedBuffer
        );

        const decoder = new TextDecoder();
        return decoder.decode(decryptedBuffer);
    }

    /**
     * 加密文件
     * @param {ArrayBuffer} fileBuffer - 文件数据
     * @param {function(number): void} progressCallback - 进度回调（百分比）
     * @returns {Promise<{encryptedData: ArrayBuffer, iv: string, key: string}>}
     */
    async encryptFile(fileBuffer, progressCallback) {
        if (!this.key) {
            throw new Error('密钥未初始化，请先调用 deriveKey');
        }

        const iv = crypto.getRandomValues(new Uint8Array(12));
        const totalChunks = Math.ceil(fileBuffer.byteLength / (100 * 1024 * 1024)); // 100MB chunks
        let processedChunks = 0;

        // 对于大文件，分块加密
        const chunkSize = 100 * 1024 * 1024; // 100MB
        const encryptedChunks = [];

        for (let i = 0; i < fileBuffer.byteLength; i += chunkSize) {
            const chunk = fileBuffer.slice(i, Math.min(i + chunkSize, fileBuffer.byteLength));

            const encryptedChunk = await crypto.subtle.encrypt(
                { name: 'AES-GCM', iv: iv },
                this.key,
                chunk
            );

            encryptedChunks.push(new Uint8Array(encryptedChunk));

            processedChunks++;
            if (progressCallback) {
                const progress = Math.round((processedChunks / totalChunks) * 100);
                progressCallback(progress);
            }
        }

        // 合并加密的块
        const totalLength = encryptedChunks.reduce((sum, chunk) => sum + chunk.length, 0);
        const encryptedBuffer = new Uint8Array(totalLength);
        let offset = 0;
        for (const chunk of encryptedChunks) {
            encryptedBuffer.set(chunk, offset);
            offset += chunk.length;
        }

        return {
            encryptedData: encryptedBuffer.buffer,
            iv: this.arrayBufferToBase64(iv)
        };
    }

    /**
     * 解密文件
     * @param {ArrayBuffer} encryptedBuffer - 加密的文件数据
     * @param {string} ivBase64 - Base64编码的IV
     * @param {function(number): void} progressCallback - 进度回调（百分比）
     * @returns {Promise<ArrayBuffer>} 解密后的文件数据
     */
    async decryptFile(encryptedBuffer, ivBase64, progressCallback) {
        if (!this.key) {
            throw new Error('密钥未初始化，请先调用 deriveKey');
        }

        const iv = this.base64ToArrayBuffer(ivBase64);
        const totalChunks = Math.ceil(encryptedBuffer.byteLength / (100 * 1024 * 1024)); // 100MB chunks
        let processedChunks = 0;

        // 对于大文件，分块解密
        const chunkSize = 100 * 1024 * 1024; // 100MB
        const decryptedChunks = [];

        for (let i = 0; i < encryptedBuffer.byteLength; i += chunkSize) {
            const chunk = encryptedBuffer.slice(i, Math.min(i + chunkSize, encryptedBuffer.byteLength));

            const decryptedChunk = await crypto.subtle.decrypt(
                { name: 'AES-GCM', iv: iv },
                this.key,
                chunk
            );

            decryptedChunks.push(new Uint8Array(decryptedChunk));

            processedChunks++;
            if (progressCallback) {
                const progress = Math.round((processedChunks / totalChunks) * 100);
                progressCallback(progress);
            }
        }

        // 合并解密的块
        const totalLength = decryptedChunks.reduce((sum, chunk) => sum + chunk.length, 0);
        const decryptedBuffer = new Uint8Array(totalLength);
        let offset = 0;
        for (const chunk of decryptedChunks) {
            decryptedBuffer.set(chunk, offset);
            offset += chunk.length;
        }

        return decryptedBuffer.buffer;
    }

    /**
     * 保存密钥到 IndexedDB（使用会话密钥加密）
     * @param {string} sessionToken - 会话令牌，用于加密主密钥
     */
    async saveKeyToIndexedDB(sessionToken) {
        if (!this.key) {
            throw new Error('没有可保存的密钥');
        }

        // 导出密钥材料（暂时导出用于加密，之后会删除）
        const exportedKey = await crypto.subtle.exportKey('raw', this.key);

        // 使用会话令牌派生临时密钥
        const encoder = new TextEncoder();
        const sessionKey = await crypto.subtle.importKey(
            'raw',
            encoder.encode(sessionToken),
            { name: 'AES-GCM' },
            false,
            ['encrypt', 'decrypt']
        );

        // 生成随机IV
        const iv = crypto.getRandomValues(new Uint8Array(12));

        // 加密主密钥
        const encryptedKey = await crypto.subtle.encrypt(
            { name: 'AES-GCM', iv: iv },
            sessionKey,
            exportedKey
        );

        // 保存到 IndexedDB
        const db = await this.openDB();
        const tx = db.transaction(this.keyStoreName, 'readwrite');
        const store = tx.objectStore(this.keyStoreName);

        await store.put({
            id: 'mainKey',
            encryptedKey: new Uint8Array(encryptedKey),
            iv: iv,
            timestamp: Date.now()
        });

        await tx.done;
        db.close();

        // 清除导出的密钥材料
        exportedKey.fill(0);
    }

    /**
     * 从 IndexedDB 加载密钥
     * @param {string} sessionToken - 会话令牌，用于解密主密钥
     */
    async loadKeyFromIndexedDB(sessionToken) {
        const db = await this.openDB();
        const tx = db.transaction(this.keyStoreName, 'readonly');
        const store = tx.objectStore(this.keyStoreName);

        const result = await store.get('mainKey');
        db.close();

        if (!result) {
            throw new Error('没有找到保存的密钥');
        }

        // 使用会话令牌派生临时密钥
        const encoder = new TextEncoder();
        const sessionKey = await crypto.subtle.importKey(
            'raw',
            encoder.encode(sessionToken),
            { name: 'AES-GCM' },
            false,
            ['encrypt', 'decrypt']
        );

        // 解密主密钥
        const decryptedKey = await crypto.subtle.decrypt(
            { name: 'AES-GCM', iv: result.iv },
            sessionKey,
            result.encryptedKey
        );

        // 导入为主密钥
        this.key = await crypto.subtle.importKey(
            'raw',
            decryptedKey,
            { name: 'AES-GCM' },
            false,
            ['encrypt', 'decrypt']
        );

        // 清除解密的密钥材料
        decryptedKey.fill(0);
    }

    /**
     * 打开 IndexedDB
     */
    async openDB() {
        return new Promise((resolve, reject) => {
            const request = indexedDB.open(this.dbName, this.dbVersion);

            request.onerror = () => reject(request.error);
            request.onsuccess = () => resolve(request.result);

            request.onupgradeneeded = (event) => {
                const db = event.target.result;
                if (!db.objectStoreNames.contains(this.keyStoreName)) {
                    db.createObjectStore(this.keyStoreName);
                }
            };
        });
    }

    /**
     * 清除 IndexedDB 中的密钥
     */
    async clearKeyFromIndexedDB() {
        const db = await this.openDB();
        const tx = db.transaction(this.keyStoreName, 'readwrite');
        const store = tx.objectStore(this.keyStoreName);
        await store.delete('mainKey');
        await tx.done;
        db.close();
        this.key = null;
    }

    /**
     * ArrayBuffer 转 Base64
     */
    arrayBufferToBase64(buffer) {
        const bytes = new Uint8Array(buffer);
        let binary = '';
        for (const byte of bytes) {
            binary += String.fromCharCode(byte);
        }
        return btoa(binary);
    }

    /**
     * Base64 转 ArrayBuffer
     */
    base64ToArrayBuffer(base64) {
        const binary = atob(base64);
        const bytes = new Uint8Array(binary.length);
        for (let i = 0; i < binary.length; i++) {
            bytes[i] = binary.charCodeAt(i);
        }
        return bytes.buffer;
    }

    /**
     * 检查是否已初始化密钥
     */
    isKeyInitialized() {
        return this.key !== null;
    }
}

// 导出为全局对象
window.E2EECrypto = E2EECrypto;
