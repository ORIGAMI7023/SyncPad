// 拖放功能的 JavaScript 互操作

window.DragDropInterop = {
    // 处理外部文件拖入
    initDropZone: function (element, dotNetRef) {
        if (!element) return;

        // 阻止默认行为以允许拖放
        element.addEventListener('dragover', (e) => {
            // 检查是否是外部文件（非内部拖动）
            if (e.dataTransfer?.types?.includes('Files')) {
                e.preventDefault();
                e.stopPropagation();
            }
        });

        element.addEventListener('drop', async (e) => {
            const files = e.dataTransfer?.files;
            if (files && files.length > 0) {
                e.preventDefault();
                e.stopPropagation();

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

                // 将文件暂存���供后续读取
                window._droppedFiles = files;

                try {
                    await dotNetRef.invokeMethodAsync('OnFilesDropped', fileInfos);
                } catch (err) {
                    console.error('调用 OnFilesDropped 失败:', err);
                }
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

    // 初始化右键菜单支持
    initContextMenu: function (containerElement, dotNetRef) {
        if (!containerElement) return;

        containerElement.addEventListener('contextmenu', (e) => {
            // 查找最近的 .file-card 父元素
            const fileCard = e.target.closest('.file-card');
            if (fileCard) {
                e.preventDefault();
                e.stopPropagation();

                // 获取文件卡片的索引（通过 data-file-id 属性）
                const fileId = parseInt(fileCard.getAttribute('data-file-id'));
                if (!isNaN(fileId)) {
                    // 通知 Blazor 显示菜单
                    dotNetRef.invokeMethodAsync('ShowContextMenuJS', fileId, e.clientX, e.clientY);
                }
            }
        });

        // 点击任意位置隐藏菜单
        document.addEventListener('click', () => {
            dotNetRef.invokeMethodAsync('HideContextMenu');
        });
    }
};
