using System;
using System.Collections.Generic;
using System.IO;
using Nexus;

namespace PD2ModelParser.Sections
{
    public class Object3D : AbstractSection, IPostLoadable, IHashContainer
    {
        public UInt32 id;
        public UInt32 size;

        public HashName hashname; //Hashed object root point name (see hashlist.txt)
        private List<uint> child_ids = new List<uint>();
        public Matrix3D rotation = new Matrix3D(); //4x4 Rotation Matrix
        public uint parentID => parent?.id ?? 0;

        public byte[] remaining_data = null;

        // Non-written fields
        private bool has_post_loaded;
        public Matrix3D world_transform;

        // Set when read from a section, before PostLoad is called
        private uint loading_parent_id;

        public Object3D parent { get; set; }

        public List<Object3D> children = new List<Object3D>();

        public string Name => hashname.String;

        public override uint SectionId
        {
            get => id;
            set => id = value;
        }

        public override uint TypeCode => Tags.object3D_tag;

        public Object3D(string object_name, Object3D parent)
        {
            this.id = 0;
            this.size = 0;

            this.hashname = new HashName(object_name);
            this.child_ids = new List<uint>();
            this.rotation = new Matrix3D(1.0f, 0.0f, 0.0f, 0.0f,
                0.0f, 1.0f, 0.0f, 0.0f,
                0.0f, 0.0f, 1.0f, 0.0f,
                0.0f, 0.0f, 0.0f, 0.0f);

            this.parent = parent;

            UpdateTransforms();
        }

        public Object3D(BinaryReader instream, SectionHeader section) : this(instream)
        {
            this.id = section.id;
            this.size = section.size;

            if (section.End > instream.BaseStream.Position)
                remaining_data = instream.ReadBytes((int) (section.End - instream.BaseStream.Position));
        }

        public Object3D(BinaryReader instream)
        {
            // In Object3D::load
            this.hashname = new HashName(instream.ReadUInt64());

            // in dsl::ParamBlock::load
            uint child_count = instream.ReadUInt32();

            for (int x = 0; x < child_count; x++)
            {
                uint item = instream.ReadUInt32(); // This is a reference thing, probably not important
                instream.ReadUInt64(); // Skip eight bytes, as per PD2
                this.child_ids.Add(item);
            }

            // In Object3D::load
            this.rotation.M11 = instream.ReadSingle();
            this.rotation.M12 = instream.ReadSingle();
            this.rotation.M13 = instream.ReadSingle();
            this.rotation.M14 = instream.ReadSingle();
            this.rotation.M21 = instream.ReadSingle();
            this.rotation.M22 = instream.ReadSingle();
            this.rotation.M23 = instream.ReadSingle();
            this.rotation.M24 = instream.ReadSingle();
            this.rotation.M31 = instream.ReadSingle();
            this.rotation.M32 = instream.ReadSingle();
            this.rotation.M33 = instream.ReadSingle();
            this.rotation.M34 = instream.ReadSingle();
            this.rotation.M41 = instream.ReadSingle();
            this.rotation.M42 = instream.ReadSingle();
            this.rotation.M43 = instream.ReadSingle();
            this.rotation.M44 = instream.ReadSingle();

            this.rotation.M41 = instream.ReadSingle();
            this.rotation.M42 = instream.ReadSingle();
            this.rotation.M43 = instream.ReadSingle();

            loading_parent_id = instream.ReadUInt32();

            this.remaining_data = null;
        }

        public override void StreamWriteData(BinaryWriter outstream)
        {
            outstream.Write(this.hashname.Hash);
            outstream.Write(child_ids.Count);
            foreach (uint item in this.child_ids)
            {
                outstream.Write(item);
                outstream.Write((ulong) 0); // Bit to skip - the PD2 binary does the exact same thing
            }

            outstream.Write(this.rotation.M11);
            outstream.Write(this.rotation.M12);
            outstream.Write(this.rotation.M13);
            outstream.Write(this.rotation.M14);
            outstream.Write(this.rotation.M21);
            outstream.Write(this.rotation.M22);
            outstream.Write(this.rotation.M23);
            outstream.Write(this.rotation.M24);
            outstream.Write(this.rotation.M31);
            outstream.Write(this.rotation.M32);
            outstream.Write(this.rotation.M33);
            outstream.Write(this.rotation.M34);
            outstream.Write(this.rotation.M41);
            outstream.Write(this.rotation.M42);
            outstream.Write(this.rotation.M43);
            outstream.Write(this.rotation.M44);
            outstream.Write(this.rotation.M41); // Write the position out again, as for some reason
            outstream.Write(this.rotation.M42); // it's not stored in the main matrix
            outstream.Write(this.rotation.M43);
            outstream.Write(this.parentID);

            if (this.remaining_data != null)
                outstream.Write(this.remaining_data);
        }

        public override string ToString()
        {
            Vector3D scale = new Vector3D();
            Quaternion rot = new Quaternion();
            Vector3D translation = new Vector3D();
            this.rotation.Decompose(out scale, out rot, out translation);
            return "[Object3D] ID: " + this.id +
                   " size: " + this.size +
                   " hashname: " + this.hashname.String +
                   " children: " + this.child_ids.Count +
                   " mat.scale: " + scale +
                   " mat.rotation: [x: " + rot.X + " y: " + rot.Y + " z: " + rot.Z + " w: " + rot.W + "]" +
                   " Parent ID: " + this.parentID +
                   (remaining_data != null ? " REMAINING DATA! " + remaining_data.Length + " bytes" : "");
        }

        public void CollectHashes(CustomHashlist hashlist)
        {
            hashlist.Hint(hashname);
        }

        public void PostLoad(uint id, Dictionary<uint, object> parsed_sections)
        {
            if (loading_parent_id == 0)
            {
                parent = null;
            }
            else
            {
                parent = (Object3D) parsed_sections[loading_parent_id];

                if (!parent.has_post_loaded)
                    parent.PostLoad(loading_parent_id, parsed_sections);

                if (!parent.children.Contains(this))
                {
                    parent.children.Add(this);
                }
            }

            UpdateTransforms();

            has_post_loaded = true;
        }

        public void UpdateTransforms()
        {
            if (parent == null)
            {
                world_transform = rotation;
                return;
            }

            world_transform = rotation.MultDiesel(parent.world_transform);
        }
    }
}
