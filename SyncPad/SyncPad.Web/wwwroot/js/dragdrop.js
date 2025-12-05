// 拖放功能的 JavaScript 互操作

window.DragDropInterop = {
    // 处理外部文件拖入
    initDropZone: function (element, dotNetRef) {
        if (!element) return;

        element.addEventListener('drop', async (e) => {
            e.preventDefault();
            e.stopPropagation();

            const files = e.dataTransfer?.files;
            if (files && files.length > 0) {
                // 获取文件信息并通知 Blazor
                const fileInfos = [];
                for (let i = 0; i < files.length; i++) {
                    const file = files[i];
                    fileInfos.push({
                        name: file.name,
                        size: file.size,
                        type: file.type || 'application/octet-stream'
                    });
                }

                // 将文件暂存以供后续读取
                window._droppedFiles = files;

                await dotNetRef.invokeMethodAsync('OnFilesDropped', fileInfos);
            }
        });

        element.addEventListener('dragover', (e) => {
            e.preventDefault();
            e.stopPropagation();
        });

        // 初始化所有文件卡片的拖出功能
        DragDropInterop.initAllFileDragOut(element);

        // 监听 DOM 变化，为新添加的文件卡片添加拖出功能
        const observer = new MutationObserver(() => {
            DragDropInterop.initAllFileDragOut(element);
        });
        observer.observe(element, { childList: true, subtree: true });
    },

    // 初始化所有文件卡片的拖出功能
    initAllFileDragOut: function (container) {
        if (!container) return;

        const cards = container.querySelectorAll('.file-card[data-download-url]');
        cards.forEach(card => {
            if (card._dragOutInitialized) return;
            card._dragOutInitialized = true;

            const downloadUrl = card.getAttribute('data-download-url');
            const fileName = card.getAttribute('data-file-name');

            if (downloadUrl && fileName) {
                card.addEventListener('dragstart', (e) => {
                    // 设置下载链接，允许拖到系统文件管理器
                    // 格式: "mime-type:filename:url"
                    e.dataTransfer.setData('DownloadURL', `application/octet-stream:${fileName}:${window.location.origin}${downloadUrl}`);
                    e.dataTransfer.setData('text/uri-list', window.location.origin + downloadUrl);
                    e.dataTransfer.setData('text/plain', fileName);
                    e.dataTransfer.effectAllowed = 'copyMove';
                });
            }
        });
    },

    // 读取拖入的文件内容（用于上传）
    readDroppedFile: async function (index) {
        const files = window._droppedFiles;
        if (!files || index >= files.length) return null;

        const file = files[index];
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = () => {
                // 返回 base64 编码的数据
                const base64 = reader.result.split(',')[1];
                resolve({
                    name: file.name,
                    type: file.type || 'application/octet-stream',
                    size: file.size,
                    data: base64
                });
            };
            reader.onerror = () => reject(reader.error);
            reader.readAsDataURL(file);
        });
    },

    // 清理暂存的文件
    clearDroppedFiles: function () {
        window._droppedFiles = null;
    },

    // 设置拖动时的数据（用于拖出到系统）
    setDragData: function (element, downloadUrl, fileName) {
        if (!element) return;

        element.addEventListener('dragstart', (e) => {
            // 设置下载链接，允许拖到系统文件管理器
            e.dataTransfer.setData('DownloadURL', `application/octet-stream:${fileName}:${downloadUrl}`);
            e.dataTransfer.setData('text/uri-list', downloadUrl);
            e.dataTransfer.effectAllowed = 'copyMove';
        });
    },

    // 初始化文件卡片的拖出功能
    initFileDragOut: function (element, downloadUrl, fileName) {
        if (!element) return;

        // 确保不会重复添加事件
        if (element._dragOutInitialized) return;
        element._dragOutInitialized = true;

        element.addEventListener('dragstart', (e) => {
            // 设置下载链接数据
            if (downloadUrl && fileName) {
                e.dataTransfer.setData('DownloadURL', `application/octet-stream:${fileName}:${downloadUrl}`);
                e.dataTransfer.setData('text/uri-list', downloadUrl);
            }
        });
    }
};
