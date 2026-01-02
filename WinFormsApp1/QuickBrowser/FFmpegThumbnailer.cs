using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using QuickBrowser;

public unsafe class FFmpegThumbnailer
{
    private const int STREAM_INDEX_NONE = -1;

    static FFmpegThumbnailer()
    {
        string dir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\";
        // 指定 FFmpeg DLL 目錄，依照安裝位置設定
        // Environment.SetEnvironmentVariable("PATH", Environment.GetEnvironmentVariable("PATH") + ";path_to_ffmpeg_bin");
        ffmpeg.RootPath = dir + "FFmpeg\\bin\\x64"; // 請設成存放 ffmpeg dlls 資料夾
    }

    public static Bitmap GetThumbnailFromVideo(string filePath, int targetSecond, int maxDimension = 512)
    {
        //ffmpeg.av_register_all();

        AVFormatContext* pFormatContext = ffmpeg.avformat_alloc_context();
        if (ffmpeg.avformat_open_input(&pFormatContext, filePath, null, null) != 0)
            throw new ApplicationException("Cannot open video file.");
        if (ffmpeg.avformat_find_stream_info(pFormatContext, null) < 0)
            throw new ApplicationException("Cannot find stream info.");

        int videoStreamIndex = -1;
        AVCodecParameters* pCodecParameters = null;
        for (int i = 0; i < pFormatContext->nb_streams; i++)
        {
            var stream = pFormatContext->streams[i];
            if (stream->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
            {
                videoStreamIndex = i;
                pCodecParameters = stream->codecpar;
                break;
            }
        }
        if (videoStreamIndex == -1)
            throw new ApplicationException("No video stream found.");

        AVCodec* pCodec = ffmpeg.avcodec_find_decoder(pCodecParameters->codec_id);
        if (pCodec == null)
            throw new ApplicationException("Unsupported codec.");

        AVCodecContext* pCodecContext = ffmpeg.avcodec_alloc_context3(pCodec);
        if (pCodecContext == null)
            throw new ApplicationException("Could not allocate codec context.");

        if (ffmpeg.avcodec_parameters_to_context(pCodecContext, pCodecParameters) < 0)
            throw new ApplicationException("Failed to copy codec parameters to decoder context.");

        if (ffmpeg.avcodec_open2(pCodecContext, pCodec, null) < 0)
            throw new ApplicationException("Failed to open codec.");

        // 依影片比例計算縮圖大小
        (int width, int height) = CalculateThumbnailSize(pCodecContext->width, pCodecContext->height, maxDimension);

        AVRational timeBase = pFormatContext->streams[videoStreamIndex]->time_base;
        long targetTimestamp = (long)(targetSecond / ffmpeg.av_q2d(timeBase));
        if (ffmpeg.av_seek_frame(pFormatContext, videoStreamIndex, targetTimestamp, ffmpeg.AVSEEK_FLAG_BACKWARD) < 0)
            throw new ApplicationException("Seeking failed.");
        ffmpeg.avcodec_flush_buffers(pCodecContext);

        AVPacket* pPacket = ffmpeg.av_packet_alloc();
        AVFrame* pFrame = ffmpeg.av_frame_alloc();

        int bufferSize = ffmpeg.av_image_get_buffer_size(AVPixelFormat.AV_PIX_FMT_BGR24, width, height, 1);
        byte* buffer = (byte*)ffmpeg.av_malloc((ulong)bufferSize);
        if (buffer == null)
            throw new ApplicationException("Could not allocate buffer.");

        byte_ptrArray4 dstData = new byte_ptrArray4();
        int_array4 dstLinesize = new int_array4();

        ffmpeg.av_image_fill_arrays(ref dstData, ref dstLinesize, buffer, AVPixelFormat.AV_PIX_FMT_BGR24, width, height, 1);

        SwsContext* pSwsCtx = ffmpeg.sws_getContext(
            pCodecContext->width,
            pCodecContext->height,
            pCodecContext->pix_fmt,
            width,
            height,
            AVPixelFormat.AV_PIX_FMT_BGR24,
            ffmpeg.SWS_BILINEAR,
            null,
            null,
            null);

        if (pSwsCtx == null)
            throw new ApplicationException("Could not initialize the conversion context.");

        Bitmap bitmap = null;
        bool frameCaptured = false;

        try
        {
            while (ffmpeg.av_read_frame(pFormatContext, pPacket) >= 0)
            {
                if (pPacket->stream_index == videoStreamIndex)
                {
                    int sendRet = ffmpeg.avcodec_send_packet(pCodecContext, pPacket);
                    ffmpeg.av_packet_unref(pPacket);
                    if (sendRet < 0)
                        continue;

                    int receiveRet = ffmpeg.avcodec_receive_frame(pCodecContext, pFrame);
                    if (receiveRet == 0)
                    {
                        ffmpeg.sws_scale(pSwsCtx,
                            pFrame->data,
                            pFrame->linesize,
                            0,
                            pCodecContext->height,
                            dstData,
                            dstLinesize);

                        bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                        var bmpData = bitmap.LockBits(new Rectangle(0, 0, width, height),
                            ImageLockMode.WriteOnly,
                            bitmap.PixelFormat);

                        for (int y = 0; y < height; y++)
                        {
                            IntPtr srcPtr = (IntPtr)(dstData[0] + y * dstLinesize[0]);
                            IntPtr destPtr = bmpData.Scan0 + y * bmpData.Stride;
                            Buffer.MemoryCopy((void*)srcPtr, (void*)destPtr, bmpData.Stride, width * 3);
                        }
                        bitmap.UnlockBits(bmpData);

                        frameCaptured = true;
                        break;
                    }
                    else if (receiveRet == ffmpeg.AVERROR(ffmpeg.EAGAIN))
                        continue;
                    else if (receiveRet == ffmpeg.AVERROR_EOF)
                        break;
                    else if (receiveRet < 0)
                        throw new ApplicationException("Error during decoding.");
                }
                else
                    ffmpeg.av_packet_unref(pPacket);
            }
        }
        finally
        {
            ffmpeg.sws_freeContext(pSwsCtx);
            ffmpeg.av_free(buffer);
            ffmpeg.av_frame_free(&pFrame);
            ffmpeg.av_packet_free(&pPacket);
            //ffmpeg.avcodec_close(pCodecContext);
            ffmpeg.avcodec_free_context(&pCodecContext);
            ffmpeg.avformat_close_input(&pFormatContext);
            ffmpeg.avformat_free_context(pFormatContext);
        }

        if (!frameCaptured)
            throw new ApplicationException("Could not capture frame.");

        return bitmap;
    }

    private static (int width, int height) CalculateThumbnailSize(int origWidth, int origHeight, int maxDimension)
    {
        if (origWidth <= 0 || origHeight <= 0)
            throw new ArgumentException("Invalid original size.");

        float ratio = origWidth / (float)origHeight;

        int width, height;
        if (origWidth >= origHeight)
        {
            width = maxDimension;
            height = (int)(maxDimension / ratio);
        }
        else
        {
            height = maxDimension;
            width = (int)(maxDimension * ratio);
        }
        return (width, height);
    }

}
